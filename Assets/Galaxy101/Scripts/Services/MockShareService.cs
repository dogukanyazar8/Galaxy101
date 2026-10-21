using System;
using UnityEngine;

namespace Galaxy101
{
    /// <summary>Local demo: copies the profile link; never opens external apps or sends messages.</summary>
    public sealed class MockShareService : IShareService
    {
        public string ProfileUrl => "https://galaxy101.app/p/DY-4821";
        public string ShareTo(string platform)
        {
            if (platform != "WhatsApp" && platform != "Telegram" && platform != "X" && platform != "Instagram" && platform != "Diğer" && platform != "More")
                throw new ArgumentException("Unknown share target.", nameof(platform));
            CopyToClipboard(); return ProfileUrl;
        }
        public void CopyToClipboard(string text = null) => GUIUtility.systemCopyBuffer = text ?? ProfileUrl;
    }
}
