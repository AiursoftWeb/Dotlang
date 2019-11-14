using System;
using System.Collections.Generic;
using System.Text;

namespace CoreTranslator.Services.BingModels
{
    public class BingResponse
    {
        public DetectedLanguage DetectedLanguage { get; set; }
        public List<TranslationsItem> Translations { get; set; }
    }
}
