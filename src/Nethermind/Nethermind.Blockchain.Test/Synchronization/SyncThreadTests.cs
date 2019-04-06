/*
 * Copyright (c) 2018 Demerzel Solutions Limited
 * This file is part of the Nethermind library.
 *
 * The Nethermind library is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * The Nethermind library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Blockchain.TransactionPools;
using Nethermind.Blockchain.Validators;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Encoding;
using Nethermind.Core.Logging;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Dirichlet.Numerics;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Stats;
using Nethermind.Store;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace Nethermind.Blockchain.Test.Synchronization
{
    [TestFixture(SynchronizerType.Fast)]
    [TestFixture(SynchronizerType.Full)]
    public class SyncThreadsTests
    {
        private readonly SynchronizerType _synchronizerType;
        private List<SyncTestContext> _peers;
        private SyncTestContext _originPeer;
        private static Block _genesis = Build.A.Block.Genesis.TestObject;

        public SyncThreadsTests(SynchronizerType synchronizerType)
        {
            _synchronizerType = synchronizerType;
        }

        private int remotePeersCount = 6;

        [SetUp]
        public void Setup()
        {
            _peers = new List<SyncTestContext>();
            for (int i = 0; i < remotePeersCount + 1; i++)
            {
                _peers.Add(CreateSyncManager(i));
            }

            _originPeer = _peers[0];
        }

        [TearDown]
        public async Task TearDown()
        {
            foreach (SyncTestContext peer in _peers)
            {
                await peer.StopAsync();
            }
        }

        [Test]
        public void Setup_is_correct()
        {
            foreach (SyncTestContext peer in _peers)
            {
                Assert.AreEqual(_genesis.Header, peer.SyncServer.Head);
            }
        }

        private void ConnectAllPeers()
        {
            for (int localIndex = 0; localIndex < _peers.Count; localIndex++)
            {
                SyncTestContext localPeer = _peers[localIndex];
                for (int remoteIndex = 0; remoteIndex < _peers.Count; remoteIndex++)
                {
                    if (localIndex == remoteIndex)
                    {
                        continue;
                    }

                    SyncTestContext remotePeer = _peers[remoteIndex];
                    localPeer.PeerPool.AddPeer(new SyncPeerMock(remotePeer.Tree, TestItem.PublicKeys[localIndex], $"PEER{localIndex}", remotePeer.SyncServer, TestItem.PublicKeys[remoteIndex], $"PEER{remoteIndex}"));
                }
            }
        }

        private const int _waitTime = 10000;

        [Test]
        public void Can_sync_when_connected()
        {
            ConnectAllPeers();

            var headBlock = ProduceBlocks(_chainLength);

            SemaphoreSlim waitEvent = new SemaphoreSlim(0);
            foreach (var peer in _peers)
            {
                peer.Tree.NewHeadBlock += (s, e) =>
                {
                    if (e.Block.Number == _chainLength) waitEvent.Release();
                };
            }

            for (int i = 0; i < _peers.Count; i++)
            {
                waitEvent.Wait(_waitTime);
            }

            for (int i = 0; i < _peers.Count; i++)
            {
                Assert.AreEqual(headBlock.Header.Number, _peers[i].SyncServer.Head.Number, i.ToString());
                Assert.AreEqual(_originPeer.StateProvider.GetBalance(headBlock.Beneficiary), _peers[i].StateProvider.GetBalance(headBlock.Beneficiary), i + " balance");
            }
        }

        private Block ProduceBlocks(int chainLength)
        {
            Block headBlock = _genesis;
            AutoResetEvent resetEvent = new AutoResetEvent(false);
            _originPeer.Tree.NewHeadBlock += (s, e) =>
            {
                resetEvent.Set();
                headBlock = e.Block;
            };
            
            for (int i = 0; i < chainLength; i++)
            {
                _originPeer.BlockProducer.ProduceEmptyBlock();
                resetEvent.WaitOne(100);
            }

            return headBlock;
        }

        private int _chainLength = 10000;
        
        [Test]
        public void Can_sync_when_initially_disconnected()
        {
            foreach (var peer in _peers)
            {
                Assert.AreEqual(_genesis.Hash, peer.SyncServer.Head.Hash, "genesis hash");
            }
            
            var headBlock = ProduceBlocks(_chainLength);
            
            SemaphoreSlim waitEvent = new SemaphoreSlim(0);
            foreach (var peer in _peers)
            {
                peer.Tree.NewHeadBlock += (s, e) =>
                {
                    if (e.Block.Number == _chainLength) waitEvent.Release();
                };
            }

            ConnectAllPeers();

            for (int i = 0; i < _peers.Count; i++)
            {
                waitEvent.Wait(_waitTime);
            }

            for (int i = 0; i < _peers.Count; i++)
            {
                Assert.AreEqual(headBlock.Header.Number, _peers[i].SyncServer.Head.Number, i.ToString());
                Assert.AreEqual(_originPeer.StateProvider.GetBalance(headBlock.Beneficiary), _peers[i].StateProvider.GetBalance(headBlock.Beneficiary), i + " balance");
            }
        }

        private class SyncTestContext
        {
            public ISyncServer SyncServer { get; set; }
            public IEthSyncPeerPool PeerPool { get; set; }
            public IBlockchainProcessor BlockchainProcessor { get; set; }
            public ISynchronizer Synchronizer { get; set; }
            public IBlockTree Tree { get; set; }
            public IStateProvider StateProvider { get; set; }
            
            public DevBlockProducer BlockProducer { get; set; }

            public async Task StopAsync()
            {
                await Synchronizer.StopAsync();
                await PeerPool.StopAsync();
                await Synchronizer.StopAsync();
            }
        }

        private SyncTestContext CreateSyncManager(int index)
        {
            Rlp.RegisterDecoders(typeof(ParityTraceDecoder).Assembly);

            // var logManager = NoErrorLimboLogs.Instance;
            var logManager = new OneLoggerLogManager(new ConsoleAsyncLogger(LogLevel.Debug, "PEER " + index));
            var specProvider = new SingleReleaseSpecProvider(ConstantinopleFix.Instance, MainNetSpecProvider.Instance.ChainId);

            MemDb traceDb = new MemDb();
            MemDb blockDb = new MemDb();
            MemDb blockInfoDb = new MemDb();
            StateDb codeDb = new StateDb();
            StateDb stateDb = new StateDb();;
            
            var stateProvider = new StateProvider(new StateTree(stateDb), codeDb, logManager);
            var storageProvider = new StorageProvider(stateDb, stateProvider, logManager);
            var receiptStorage = new InMemoryReceiptStorage();

            var ecdsa = new EthereumEcdsa(specProvider, logManager);
            var tree = new BlockTree(blockDb, blockInfoDb, specProvider, NullTransactionPool.Instance, logManager);
            var blockhashProvider = new BlockhashProvider(tree);
            var virtualMachine = new VirtualMachine(stateProvider, storageProvider, blockhashProvider, logManager);

            var sealValidator = TestSealValidator.AlwaysValid;
            var headerValidator = new HeaderValidator(tree, sealValidator, specProvider, logManager);
            var txValidator = TestTxValidator.AlwaysValid;
            var ommersValidator = new OmmersValidator(tree, headerValidator, logManager);
            var blockValidator = new BlockValidator(txValidator, headerValidator, ommersValidator, specProvider, logManager);
//            var blockValidator = TestBlockValidator.AlwaysValid;

            var rewardCalculator = new RewardCalculator(specProvider);
            var txProcessor = new TransactionProcessor(specProvider, stateProvider, storageProvider, virtualMachine, logManager);
            var blockProcessor = new BlockProcessor(specProvider, blockValidator, rewardCalculator, txProcessor, stateDb, codeDb, traceDb, stateProvider, storageProvider, NullTransactionPool.Instance, receiptStorage, logManager);
            
            var step = new TxSignaturesRecoveryStep(ecdsa, NullTransactionPool.Instance);
            var processor = new BlockchainProcessor(tree, blockProcessor, step, logManager, true, true);

            var nodeStatsManager = new NodeStatsManager(new StatsConfig(), logManager);
            var syncPeerPool = new EthSyncPeerPool(tree, nodeStatsManager, new SyncConfig(), logManager);

            StateProvider producerStateProvider = new StateProvider(new StateTree(stateDb), codeDb, logManager);
            StorageProvider producerStorageProvider = new StorageProvider(stateDb, producerStateProvider, logManager);
            var devBlockProcessor = new BlockProcessor(specProvider, blockValidator, rewardCalculator, txProcessor, stateDb, codeDb, traceDb, producerStateProvider, producerStorageProvider, NullTransactionPool.Instance, receiptStorage, logManager);
            var devChainProcessor = new BlockchainProcessor(tree, devBlockProcessor, step, logManager, false, false);
            var producer = new DevBlockProducer(NullTransactionPool.Instance, devChainProcessor, tree, new Timestamp(), logManager);
            
            ISynchronizer synchronizer;
            switch (_synchronizerType)
            {
                case SynchronizerType.Full:
                    synchronizer = new FullSynchronizer(
                        tree,
                        blockValidator,
                        sealValidator,
                        txValidator,
                        syncPeerPool, new SyncConfig(), logManager);
                    break;
                case SynchronizerType.Fast:
                    NodeDataDownloader downloader = new NodeDataDownloader(codeDb, stateDb, logManager);
                    synchronizer = new FastSynchronizer(
                        tree,
                        headerValidator,
                        sealValidator,
                        txValidator,
                        syncPeerPool, new SyncConfig(), downloader, logManager);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var syncServer = new SyncServer(stateDb, tree, receiptStorage, TestSealValidator.AlwaysValid, syncPeerPool, synchronizer, logManager);

            ManualResetEventSlim waitEvent = new ManualResetEventSlim();
            tree.NewHeadBlock += (s, e) => waitEvent.Set();

            syncPeerPool.Start();
            synchronizer.Start();
            processor.Start();
            tree.SuggestBlock(_genesis);

            if (!waitEvent.Wait(20000))
            {
                throw new Exception("No genesis");
            }

            SyncTestContext context = new SyncTestContext();
            context.BlockchainProcessor = processor;
            context.PeerPool = syncPeerPool;
            context.StateProvider = stateProvider;
            context.Synchronizer = synchronizer;
            context.SyncServer = syncServer;
            context.Tree = tree;
            context.BlockProducer = producer;
            return context;
        }
    }
}