//  Copyright (c) 2020 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Nethermind.Vault.Styles
{
    public class KeyArgs
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        
        [JsonProperty("usage")]
        public string Usage { get; set; }
        
        [JsonProperty("spec")]
        public string Spec { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("description")]
        public string Description { get; set; }
        
        public static KeyArgs Default = new KeyArgs();

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object> 
            {
                { nameof(Type), Type },
                { nameof(Usage), Usage },
                { nameof(Spec), Spec },
                { nameof(Name), Name },
                { nameof(Description), Description }
            };
        }
    }
}