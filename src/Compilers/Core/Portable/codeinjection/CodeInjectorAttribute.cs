using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.CodeInjection
{
    /// <summary>
    /// Place this attribute onto a type to cause it to be considered a <see cref="CodeInjector"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CodeInjectorAttribute : Attribute
    {
        /// <summary>
        /// The source languages to which this <see cref="CodeInjector"/> applies.  See <see cref="LanguageNames"/>.
        /// </summary>
        public string[] Languages { get; }

        /// <summary>
        /// Attribute constructor used to specify automatic application of a <see cref="CodeInjector"/>.
        /// </summary>
        /// <param name="firstLanguage">One language to which the <see cref="CodeInjector"/> applies.</param>
        /// <param name="additionalLanguages">Additional languages to which the <see cref="CodeInjector"/> applies. See <see cref="LanguageNames"/>.</param>
        public CodeInjectorAttribute(string firstLanguage, params string[] additionalLanguages)
        {
            if (firstLanguage == null)
            {
                throw new ArgumentNullException(nameof(firstLanguage));
            }

            if (additionalLanguages == null)
            {
                throw new ArgumentNullException(nameof(additionalLanguages));
            }

            var languages = new string[additionalLanguages.Length + 1];
            languages[0] = firstLanguage;
            for (int index = 0; index < additionalLanguages.Length; index++)
            {
                languages[index + 1] = additionalLanguages[index];
            }

            this.Languages = languages;
        }
    }
}
