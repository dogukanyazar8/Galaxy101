using System;
using System.IO;
using UnityEngine;

namespace Galaxy101
{
    public static class GalaxyPreferencesBridge
    {
        public const string StateFileName="galaxy-menu.json";
        /// <summary>Development state router (G101StateRouter): every state run starts from its own empty save.</summary>
        public static string OverridePath;
        public static string ResolveStatePath(string explicitPath=null)
        {
            if(explicitPath!=null)return Path.GetFullPath(explicitPath);
            if(OverridePath!=null)return Path.GetFullPath(OverridePath);
#if UNITY_EDITOR || OKEY_DIAGNOSTICS
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-galaxyMenuState");
            if(flag>=0)
            {
                if(flag+1>=args.Length||!Path.IsPathRooted(args[flag+1]))throw new ArgumentException("-galaxyMenuState requires an absolute diagnostic state path.");
                return Path.GetFullPath(args[flag+1]);
            }
#endif
            return Path.Combine(Application.persistentDataPath,StateFileName);
        }
        public static bool TryLoadMasterVolume(out float volume,out string error,string path=null)
        {
            volume=1;error=null;path=ResolveStatePath(path);
            if(!File.Exists(path))return false;
            var model=MenuModel.LoadResources(path);
            if(model.LastPersistenceError!=null){error=model.LastPersistenceError;return false;}
            volume=(int)model.Settings["master"]/100f;return true;
        }
        public static void SaveMasterVolume(float volume,string path=null)
        {
            if(float.IsNaN(volume)||float.IsInfinity(volume)||volume<0||volume>1)throw new ArgumentOutOfRangeException(nameof(volume));
            path=ResolveStatePath(path);if(!File.Exists(path))return;
            var model=MenuModel.LoadResources(path);
            if(model.LastPersistenceError!=null)throw new IOException("Galaxy menu preferences could not be read: "+model.LastPersistenceError);
            model.Settings["master"]=(int)Math.Round(volume*100,MidpointRounding.AwayFromZero);
            if(!model.Persist())throw new IOException("Galaxy menu volume could not be saved: "+model.LastPersistenceError);
        }
    }
}
