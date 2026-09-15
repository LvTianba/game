using System;
using UnityEngine;

namespace BorderValley.Presentation
{
    [Serializable]
    public sealed class StringPair
    {
        [SerializeField] private string key;
        [SerializeField] private string value;

        public StringPair()
        {
        }

        public StringPair(string key, string value)
        {
            this.key = key;
            this.value = value;
        }

        public string Key => key;
        public string Value => value;
    }
}
