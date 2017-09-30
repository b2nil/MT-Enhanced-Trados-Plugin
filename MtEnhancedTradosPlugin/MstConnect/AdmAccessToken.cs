﻿using System.Runtime.Serialization;

namespace MstTranslateConnect
{
    [DataContract]
    internal class AdmAccessToken
    {
        [DataMember]
        internal string access_token { get; set; }
        [DataMember]
        internal string token_type { get; set; }
        [DataMember]
        internal string expires_in { get; set; }
        [DataMember]
        internal string scope { get; set; }

    }
}
