using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Okey101.Application
{
    public sealed class LocalSettings
    {
        public float MasterVolume { get; set; } = 1;
        public bool Fullscreen { get; set; }
        public bool ReducedMotion { get; set; }
        [DataContract] private sealed class SettingsDto
        {
            [DataMember(IsRequired=true)] public int Version;
            [DataMember(IsRequired=true)] public float MasterVolume;
            [DataMember(IsRequired=true)] public bool Fullscreen;
            [DataMember(IsRequired=true)] public bool ReducedMotion;
        }
        private static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Okey101","settings.json");
        private static DataContractJsonSerializer Serializer() => new DataContractJsonSerializer(typeof(SettingsDto),
            new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 16 });
        private static void Validate(float volume)
        { if(float.IsNaN(volume)||float.IsInfinity(volume)||volume<0||volume>1)throw new ArgumentOutOfRangeException(nameof(MasterVolume)); }
        public static LocalSettings Load(string path = null, Action<string> log = null)
        {
            try
            {
                using(var file=new FileStream(path??DefaultPath,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(file.Length>65536)throw new SerializationException("Settings exceed 64 KiB.");
                    var data=(SettingsDto)Serializer().ReadObject(file);
                    if(data==null||data.Version!=1)throw new SerializationException("Unsupported settings schema.");
                    Validate(data.MasterVolume);
                    return new LocalSettings { MasterVolume=data.MasterVolume,Fullscreen=data.Fullscreen,ReducedMotion=data.ReducedMotion };
                }
            }
            catch(FileNotFoundException) { return new LocalSettings(); }
            catch(DirectoryNotFoundException) { return new LocalSettings(); }
            catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException
                || exception is SerializationException || exception is ArgumentException || exception is System.Xml.XmlException)
            {
                var message="Settings load failed; safe defaults used. "+exception.GetType().Name+": "+exception.Message;
                if(log!=null)log(message);else System.Diagnostics.Trace.TraceWarning(message);
                return new LocalSettings();
            }
        }
        public void Save(string path = null)
        {
            Validate(MasterVolume); path=Path.GetFullPath(path??DefaultPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {
                    Serializer().WriteObject(file,new SettingsDto { Version=1,MasterVolume=MasterVolume,Fullscreen=Fullscreen,ReducedMotion=ReducedMotion });
                    file.Flush(true);
                }
                if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
            }
            finally { if(File.Exists(temporary))File.Delete(temporary); }
        }
    }
}
