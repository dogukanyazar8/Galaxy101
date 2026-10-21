using System;
using System.Reflection;
using NUnit.Framework;

namespace Galaxy101.Tests
{
    public sealed class PanelFormTests
    {
        private static object Invoke(string type, string method, params object[] arguments)
        {
            var target = Type.GetType("Galaxy101." + type + ", Galaxy101.Runtime");
            Assert.That(target, Is.Not.Null, "Missing native panel controller " + type);
            var member = target.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            Assert.That(member, Is.Not.Null, "Missing form guard " + method);
            return member.Invoke(null, arguments);
        }

        [TestCase("  Doğukan Y.  ", true)]
        [TestCase("İpek_32", true)]
        [TestCase("ab", false)]
        [TestCase("12345678901234567", false)]
        [TestCase("ad<script>", false)]
        public void ProfileSaveRequiresValidTrimmedUnicodeName(string name, bool expected)
        {
            Assert.That(Invoke("EditProfileController", "IsNameValid", name), Is.EqualTo(expected));
        }

        [TestCase("sil", "tr", true)]
        [TestCase(" SİL ", "tr", true)]
        [TestCase("DELETE", "tr", false)]
        [TestCase(" delete ", "en", true)]
        [TestCase("SİL", "en", false)]
        [TestCase("", "tr", false)]
        public void AccountDeleteRequiresLocalizedExactConfirmation(string typed, string language, bool expected)
        {
            Assert.That(Invoke("ConfirmController", "CanDelete", typed, language), Is.EqualTo(expected));
        }
    }
}

