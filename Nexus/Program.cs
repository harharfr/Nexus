using Injector;
using Newtonsoft.Json.Linq;
using Suspend;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        bool headless = args.Any(a => a.Equals("-headless", StringComparison.OrdinalIgnoreCase));
        Run(headless).GetAwaiter().GetResult();
    }

    static async Task Run(bool headless)
    {
        if (!PickFortniteFolders(out string fortniteGame, out string engine))
            return;

        Console.WriteLine("FortniteGame: " + fortniteGame);
        Console.WriteLine("Engine: " + engine);

        await EpicLogin(fortniteGame, engine, headless);
    }

    static bool PickFortniteFolders(out string fortniteGame, out string engine)
    {
        fortniteGame = null;
        engine = null;

        using (FolderBrowserDialog dialog = new FolderBrowserDialog())
        {
            dialog.Description = "Select your Fortnite installation folder";
            dialog.ShowNewFolderButton = false;

            if (dialog.ShowDialog() != DialogResult.OK)
                return false;

            string root = dialog.SelectedPath;

            string fortniteGamePath = Path.Combine(root, "FortniteGame");
            string enginePath = Path.Combine(root, "Engine");

            if (!Directory.Exists(fortniteGamePath) || !Directory.Exists(enginePath))
            {
                Console.WriteLine("Invalid Fortnite installation folder.");
                return false;
            }

            string fortniteExe = Path.Combine(
                fortniteGamePath,
                "Binaries",
                "Win64",
                "FortniteClient-Win64-Shipping.exe"
            );

            if (!File.Exists(fortniteExe))
            {
                Console.WriteLine("Fortnite executable not found.");
                return false;
            }

            fortniteGame = fortniteGamePath;
            engine = enginePath;
            return true;
        }
    }

    static async Task EpicLogin(string fortnitePath, string enginePath, bool headless)
    {
        if (headless)
        {
            StartServer(fortnitePath, enginePath, headless);
            return;
        }


        using (HttpClient http = new HttpClient())
        {
            HttpRequestMessage tokenReq = new HttpRequestMessage(
                HttpMethod.Post,
                "https://account-public-service-prod.ol.epicgames.com/account/api/oauth/token"
            );

            tokenReq.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Basic",
                    "ZDg1NjZmMmU3ZjVjNDhmODk2ODMxNzNlYjUyOWZlZTE6MjU1YzcxMDljODI3NDI0MTk4NjYxNmUzNzAyNjc4YjU="
                );

            tokenReq.Content = new StringContent(
                "grant_type=client_credentials",
                Encoding.UTF8,
                "application/x-www-form-urlencoded"
            );

            HttpResponseMessage tokenRes = await http.SendAsync(tokenReq);
            tokenRes.EnsureSuccessStatusCode();

            JObject tokenJson = JObject.Parse(await tokenRes.Content.ReadAsStringAsync());
            string accessToken = tokenJson["access_token"].ToString();

            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage deviceRes = await http.PostAsync(
                "https://account-public-service-prod03.ol.epicgames.com/account/api/oauth/deviceAuthorization",
                new StringContent("prompt=login", Encoding.UTF8, "application/x-www-form-urlencoded")
            );

            JObject deviceJson = JObject.Parse(await deviceRes.Content.ReadAsStringAsync());

            string deviceCode = deviceJson["device_code"].ToString();
            string userCode = deviceJson["user_code"].ToString();

            Process.Start("https://www.epicgames.com/id/activate?userCode=" + userCode);

            Console.WriteLine("Waiting for Epic Games login...");

            DateTime start = DateTime.UtcNow;

            while ((DateTime.UtcNow - start).TotalSeconds < 120)
            {
                await Task.Delay(5000);

                HttpRequestMessage pollReq = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://account-public-service-prod.ol.epicgames.com/account/api/oauth/token"
                );

                pollReq.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Basic",
                        "ZDg1NjZmMmU3ZjVjNDhmODk2ODMxNzNlYjUyOWZlZTE6MjU1YzcxMDljODI3NDI0MTk4NjYxNmUzNzAyNjc4YjU="
                    );

                pollReq.Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string,string>("grant_type","device_code"),
                    new KeyValuePair<string,string>("device_code",deviceCode),
                    new KeyValuePair<string,string>("token_type","eg1")
                });

                HttpResponseMessage pollRes = await http.SendAsync(pollReq);

                if (!pollRes.IsSuccessStatusCode)
                    continue;

                JObject pollJson = JObject.Parse(await pollRes.Content.ReadAsStringAsync());
                string deviceAccessToken = pollJson["access_token"].ToString();

                using (HttpClient exchangeHttp = new HttpClient())
                {
                    exchangeHttp.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", deviceAccessToken);

                    HttpResponseMessage exchangeRes = await exchangeHttp.GetAsync(
                        "https://account-public-service-prod.ol.epicgames.com/account/api/oauth/exchange?consumingClientId=ec684b8c687f479fadea3cb2ad83f5c6"
                    );

                    if (!exchangeRes.IsSuccessStatusCode)
                        throw new Exception("Failed to generate exchange code");

                    JObject exchangeJson = JObject.Parse(await exchangeRes.Content.ReadAsStringAsync());
                    string exchangeCode = exchangeJson["code"].ToString();

                    LaunchApp(fortnitePath, enginePath, exchangeCode);
                    return;
                }
            }
        }
    }

    static void StartServer(string fortnitePath, string enginePath, bool headless)
    {
        string binaries = Path.Combine(fortnitePath, "Binaries", "Win64");
        Directory.SetCurrentDirectory(binaries);

        string Args = $"-AUTH_LOGIN=S13HostMain@epic.dev -AUTH_PASSWORD=1337haha -AUTH_TYPE=Epic -epicapp=Fortnite -epicenv=Prod -EpicPortal -skippatchcheck -nobe -fromfl=eac -fltoken=7a848a93a74ba68876c36C1c -nullrhi -nosound";

        Process FNL = Process.Start("FortniteLauncher.exe");
        FNL.Suspend();

        Process EAC = Process.Start("FortniteClient-Win64-Shipping_EAC.exe");
        EAC.Suspend();

        Process Shipping = Process.Start("FortniteClient-Win64-Shipping.exe", Args);

        Shipping.WaitForInputIdle();

        Injection.Inject(Shipping.Id, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nexus", "Curl2.dll"));

        Thread.Sleep(50000);
        Injection.Inject(Shipping.Id, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nexus", "Ingame.dll"));

        Shipping.WaitForExit();

        if (!EAC.HasExited) EAC.Kill();
        if (!FNL.HasExited) FNL.Kill();

    }

    static void LaunchApp(string fortnitePath, string enginePath, string token)
    {
        string binaries = Path.Combine(fortnitePath, "Binaries", "Win64");
        Directory.SetCurrentDirectory(binaries);

        string Args = $"-AUTH_LOGIN=unused -AUTH_PASSWORD={token} -AUTH_TYPE=exchangecode -epicapp=Fortnite -epicenv=Prod -EpicPortal -skippatchcheck -nobe -fromfl=eac -fltoken=7a848a93a74ba68876c36C1c";

        Process FNL = Process.Start("FortniteLauncher.exe");
        FNL.Suspend();

        Process EAC = Process.Start("FortniteClient-Win64-Shipping_EAC.exe");
        EAC.Suspend();

        Process Shipping = Process.Start("FortniteClient-Win64-Shipping.exe", Args);

        Shipping.WaitForInputIdle();

        Injection.Inject(Shipping.Id, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nexus", "Curl.dll"));



        Shipping.WaitForExit();

        if (!EAC.HasExited) EAC.Kill();
        if (!FNL.HasExited) FNL.Kill();

    }
}