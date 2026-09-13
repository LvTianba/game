using UnityEngine;

namespace BorderValley.Data
{
    public readonly struct ContentValidationIssue
    {
        public ContentValidationIssue(string code, string message, Object context)
        {
            Code = code;
            Message = message;
            Context = context;
        }

        public string Code { get; }
        public string Message { get; }
        public Object Context { get; }
    }
}
