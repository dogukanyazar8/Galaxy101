using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Galaxy101.Online;
using NUnit.Framework;

namespace Galaxy101.Tests
{
    /// <summary>The desktop Google flow end to end on loopback: the test plays the browser that Google redirects.</summary>
    public sealed class GoogleDesktopSignInTests
    {
        private sealed class Attempt
        {
            public Task<GoogleCodeRequest> Task;
            public string Redirect, State, Challenge, Url;
        }

        private static Attempt Start(CancellationToken cancel = default)
        {
            string url = null;
            // Off the editor's main thread: the flow awaits sockets, and the test blocks on its result.
            var task = Task.Run(() => GoogleDesktopSignIn.AuthorizeAsync("cid.apps.googleusercontent.com", false, cancel, u => url = u));
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (url == null && DateTime.UtcNow < deadline && !task.IsCompleted) Thread.Sleep(10);
            Assert.That(url, Is.Not.Null, "the consent page was opened");
            var query = new Uri(url).Query.TrimStart('?').Split('&').Select(p => p.Split('=')).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
            return new Attempt { Task = task, Url = url, Redirect = query["redirect_uri"], State = query["state"], Challenge = query["code_challenge"] };
        }

        /// <summary>A browser request to the loopback listener: the status line and the body.</summary>
        private static (int Status, string Body) Get(string url)
        {
            var uri = new Uri(url);
            using (var client = new TcpClient())
            {
                client.Connect(IPAddress.Loopback, uri.Port);
                var stream = client.GetStream();
                var request = Encoding.ASCII.GetBytes("GET " + uri.PathAndQuery + " HTTP/1.1\r\nHost: 127.0.0.1:" + uri.Port + "\r\n\r\n");
                stream.Write(request, 0, request.Length);
                var buffer = new byte[16384]; int read = 0, n;
                while ((n = stream.Read(buffer, read, buffer.Length - read)) > 0) read += n;
                var text = Encoding.UTF8.GetString(buffer, 0, read);
                return (int.Parse(text.Split(' ')[1]), WebUtility.HtmlDecode(text.Substring(text.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)));
            }
        }

        [Test]
        public void TheCodeComesBackWithAMatchingPkceVerifierAndOnlyForThisAttemptsState()
        {
            var a = Start();
            StringAssert.StartsWith("https://accounts.google.com/o/oauth2/v2/auth?response_type=code", a.Url);
            StringAssert.Contains("code_challenge_method=S256", a.Url);
            StringAssert.Contains("client_id=cid.apps.googleusercontent.com", a.Url);
            StringAssert.Contains("scope=openid%20email%20profile", a.Url);
            StringAssert.StartsWith("http://127.0.0.1:", a.Redirect);

            Assert.That(Get(a.Redirect + "favicon.ico").Status, Is.EqualTo(404), "stray browser requests are ignored");
            Assert.That(Get(a.Redirect + "?state=forged&code=stolen").Status, Is.EqualTo(400), "another attempt's answer is refused");
            Assert.That(a.Task.IsCompleted, Is.False);

            var page = Get(a.Redirect + "?state=" + Uri.EscapeDataString(a.State) + "&code=4%2F0AbC&scope=openid");
            Assert.That(page.Status, Is.EqualTo(200));
            StringAssert.Contains("Giriş yapıldı", page.Body);
            Assert.That(a.Task.Wait(5000), Is.True);
            var result = a.Task.Result;
            Assert.That(result.Code, Is.EqualTo("4/0AbC"));
            Assert.That(result.RedirectUri, Is.EqualTo(a.Redirect));
            Assert.That(result.CodeVerifier.Length, Is.EqualTo(43));
            using (var sha = SHA256.Create())
                Assert.That(GoogleDesktopSignIn.Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(result.CodeVerifier))), Is.EqualTo(a.Challenge));
        }

        [Test]
        public void ChoosingCancelOnGooglesPageEndsTheAttempt()
        {
            var a = Start();
            var page = Get(a.Redirect + "?state=" + Uri.EscapeDataString(a.State) + "&error=access_denied");
            StringAssert.Contains("Giriş iptal edildi", page.Body);
            var error = Assert.Throws<AggregateException>(() => a.Task.Wait(5000)).InnerException as GoogleSignInCancelledException;
            Assert.That(error, Is.Not.Null); Assert.That(error.TimedOut, Is.False);
        }

        [Test]
        public void PressingTheButtonAgainCancelsAndFreesThePort()
        {
            var cancel = new CancellationTokenSource();
            var a = Start(cancel.Token);
            cancel.Cancel();
            var error = Assert.Throws<AggregateException>(() => a.Task.Wait(5000)).InnerException as GoogleSignInCancelledException;
            Assert.That(error, Is.Not.Null); Assert.That(error.TimedOut, Is.False);
            Assert.Throws<SocketException>(() => Get(a.Redirect + "?state=x&code=y"), "the listener is closed");
        }
    }
}
