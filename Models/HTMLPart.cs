using System;
using System.Collections.Generic;
using System.Text;

namespace CoreTranslator.Models
{
    public enum StringType
    {
        Tag,
        Razor,
        Text
    }
    public class HTMLPart
    {
        public StringType StringType { get; set; }
        public string Content { get; set; }
        public override string ToString() => this.Content;
    }
}
