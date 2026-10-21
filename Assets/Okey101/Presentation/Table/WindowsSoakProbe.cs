#if UNITY_EDITOR || OKEY_DIAGNOSTICS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.Profiling;
using UnityEngine;
namespace Okey101.Presentation
{
    // Explicit opt-in automatic acceptance workload; excluded from Release.
    public sealed class WindowsSoakProbe : MonoBehaviour
    {
        [Serializable] private sealed class Evidence
        {
            public bool completed,frameTimingSupported,performanceTargetMet,backgroundMode,backgroundFrameTargetMet;
            public string failure="",startedUtc,completedUtc,unity,productVersion,ruleVersion,cpu,gpu,graphics,quality;
            public int ramMB,width,height,vSyncCount,targetFrameRate,profileSamples,timingSamples,gpuSamples,renderedFrames,rounds,commands,pauseCycles,errors,exceptions;
            public double offscreenRenderCpuMeanMs;
            public double elapsedSeconds,profileElapsedSeconds,focusLostSeconds,activeSeconds;
            public double frameMeanMs,frameP95Ms,frameMaxMs,cpuMeanMs,cpuP95Ms,gpuMeanMs,gpuP95Ms,mainMeanMs,mainP95Ms,renderMeanMs,presentWaitMeanMs;
            public long managedStartBytes,managedEndBytes,managedPeakBytes,workingSetStartBytes,workingSetEndBytes,workingSetPeakBytes;
            public Sample[] slowestFrames;
        }
        [Serializable] private struct Sample
        { public double second,frameMs,cpuMs,gpuMs,mainMs,renderMs,presentMs,offscreenRenderCpuMs;public long version;public bool botThinking;public int gc0; }
        private readonly List<Sample> samples=new List<Sample>(40000);
        private readonly FrameTiming[] timing=new FrameTiming[1];
        private GameBootstrap game;
        private Evidence evidence;
        private StreamWriter frames,memory;
        private System.Diagnostics.Process process;
        private string directory;
        private double started,warmup,profile,total,lastFrame,nextMemory,nextPause,pauseUntil;
        private long version;
        private Guid round;
        private Task work;
        private bool initialized,finished,ownsPause;
        private OffscreenAcceptanceView offscreen;
        public void Initialize(GameBootstrap owner,string output,double warmupSeconds=30,double profileSeconds=600,double totalSeconds=1800)
        {
            if(initialized)throw new InvalidOperationException("A soak probe can only start once.");
            if(double.IsNaN(warmupSeconds)||double.IsNaN(profileSeconds)||double.IsNaN(totalSeconds)
                ||double.IsInfinity(totalSeconds)||warmupSeconds<0||profileSeconds<=0||totalSeconds<warmupSeconds+profileSeconds||totalSeconds>7200)
                throw new ArgumentOutOfRangeException(nameof(totalSeconds));
            directory=Path.GetFullPath(output);
            if(!Directory.Exists(directory)||Directory.EnumerateFileSystemEntries(directory).Any())
                throw new IOException("A4 evidence requires an existing empty output directory.");
            game=owner??throw new ArgumentNullException(nameof(owner));
            if(game.Session==null)throw new InvalidOperationException("A4 requires an initialized table.");
            warmup=warmupSeconds;profile=profileSeconds;total=totalSeconds;
            offscreen=game.GetComponent<OffscreenAcceptanceView>();
            process=System.Diagnostics.Process.GetCurrentProcess();
            frames=new StreamWriter(new FileStream(Path.Combine(directory,"frames.csv"),FileMode.CreateNew,FileAccess.Write,FileShare.Read),System.Text.Encoding.UTF8,65536);
            memory=new StreamWriter(new FileStream(Path.Combine(directory,"memory.csv"),FileMode.CreateNew,FileAccess.Write,FileShare.Read));
            frames.WriteLine("second,frameMs,cpuMs,gpuMs,mainMs,renderMs,presentMs,version,botThinking,gc0,offscreenRenderCpuMs");
            memory.WriteLine("second,managedBytes,unityAllocatedBytes,unityReservedBytes,workingSetBytes,owners,motions,audioSources,rounds,commands,paused");
            started=lastFrame=Time.realtimeSinceStartupAsDouble;nextPause=started+60;nextMemory=started;
            var state=game.Session.GetView(0);round=state.RoundId;version=state.Version;
            evidence=new Evidence {startedUtc=DateTime.UtcNow.ToString("O"),unity=UnityEngine.Application.unityVersion,
                backgroundMode=BackgroundAcceptance.Requested,
                productVersion=UnityEngine.Application.version,ruleVersion=Okey101.Application.ReplayRecord.RuleVersion,
                cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,ramMB=SystemInfo.systemMemorySize,
                graphics=SystemInfo.graphicsDeviceType.ToString(),quality=QualitySettings.names[QualitySettings.GetQualityLevel()],
                width=Screen.width,height=Screen.height,vSyncCount=QualitySettings.vSyncCount,targetFrameRate=UnityEngine.Application.targetFrameRate,
                frameTimingSupported=FrameTimingManager.IsFeatureEnabled()};
            initialized=true;UnityEngine.Application.logMessageReceived+=OnLog;
            Debug.Log("A4_SOAK_STARTED "+directory);
        }
        private void OnLog(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Assert)evidence.errors++;if(type==LogType.Exception)evidence.exceptions++;}
        private void Update()
        {
            if(!initialized||finished)return;
            try{Tick();}
            catch(Exception exception){Finish(exception.GetType().Name+": "+exception.Message);Debug.LogException(exception);}
        }
        private void Tick()
        {
            var now=Time.realtimeSinceStartupAsDouble;var elapsed=now-started;var delta=now-lastFrame;lastFrame=now;
            if(!UnityEngine.Application.isFocused)evidence.focusLostSeconds+=delta;
            if(!game.Session.IsPaused)evidence.activeSeconds+=delta;
            if(work!=null&&work.IsCompleted){work.GetAwaiter().GetResult();work=null;game.SetBotsEnabled(true);}
            var state=game.Session.GetView(0);
            if(state.RoundId!=round){round=state.RoundId;version=0;evidence.rounds++;}
            evidence.commands+=checked((int)(state.Version-version));version=state.Version;
            if(elapsed>=warmup&&elapsed<warmup+profile)
            {
                if(samples.Count>=200000)throw new InvalidOperationException("A4 sample safety bound exceeded.");
                var sample=new Sample {second=elapsed,frameMs=delta*1000,version=version,botThinking=game.BotThinking,gc0=GC.CollectionCount(0),offscreenRenderCpuMs=offscreen!=null?offscreen.LastRenderCpuMs:0};
                if(FrameTimingManager.GetLatestTimings(1,timing)>0)
                {sample.cpuMs=timing[0].cpuFrameTime;sample.gpuMs=timing[0].gpuFrameTime;sample.mainMs=timing[0].cpuMainThreadFrameTime;sample.renderMs=timing[0].cpuRenderThreadFrameTime;sample.presentMs=timing[0].cpuMainThreadPresentWaitTime;}
                samples.Add(sample);
                frames.WriteLine(FormattableString.Invariant($"{sample.second:F6},{sample.frameMs:F6},{sample.cpuMs:F6},{sample.gpuMs:F6},{sample.mainMs:F6},{sample.renderMs:F6},{sample.presentMs:F6},{sample.version},{sample.botThinking},{sample.gc0},{sample.offscreenRenderCpuMs:F6}"));
            }
            FrameTimingManager.CaptureFrameTimings();
            if(now>=nextMemory){RecordMemory(elapsed);nextMemory=now+15;}
            if(elapsed>=total)
            {
                // Finish an already accepted, moving command; never start another one.
                // A paused or stalled task must still produce a bounded failure report.
                if(work!=null&&!game.Session.IsPaused&&elapsed<total+15)return;
                RecordMemory(elapsed);Finish(work!=null?"Pending work at duration boundary.":"");return;
            }
            if(ownsPause&&now>=pauseUntil){game.SetPaused(false);ownsPause=false;}
            if(work!=null)return;
            if(now>=nextPause&&!game.Session.IsPaused&&game.CanControl)
            {game.SetPaused(true);ownsPause=true;pauseUntil=now+2;nextPause=now+60;evidence.pauseCycles++;return;}
            if(game.Session.IsPaused)return;
            if(state.Result!=null){work=game.StartNextRoundAsync();return;}
            if(game.CanControl&&state.ActivePlayer==0)work=game.StepDiagnosticBotAsync();
        }
        private void RecordMemory(double elapsed)
        {
            var managed=GC.GetTotalMemory(false);process.Refresh();var working=process.WorkingSet64;
            if(evidence.managedStartBytes==0){evidence.managedStartBytes=managed;evidence.workingSetStartBytes=working;}
            evidence.managedEndBytes=managed;evidence.workingSetEndBytes=working;
            evidence.managedPeakBytes=Math.Max(evidence.managedPeakBytes,managed);evidence.workingSetPeakBytes=Math.Max(evidence.workingSetPeakBytes,working);
            var owners=FindObjectsByType<GameBootstrap>().Count(g=>g.Session!=null);
            var motions=FindObjectsByType<TileMotion>().Length;
            var audio=FindObjectsByType<AudioSource>().Length;
            memory.WriteLine(FormattableString.Invariant($"{elapsed:F3},{managed},{Profiler.GetTotalAllocatedMemoryLong()},{Profiler.GetTotalReservedMemoryLong()},{working},{owners},{motions},{audio},{evidence.rounds},{evidence.commands},{game.Session.IsPaused}"));memory.Flush();
            if(owners!=1||motions!=1||audio>1)throw new InvalidOperationException("A4 found duplicate/orphan session, motion or audio ownership.");
            Debug.Log(FormattableString.Invariant($"A4_PROGRESS seconds={elapsed:F1} rounds={evidence.rounds} commands={evidence.commands} managedMB={managed/1048576.0:F1} workingMB={working/1048576.0:F1} owners={owners} motions={motions} audio={audio}"));
        }
        private void Finish(string failure)
        {
            if(finished)return;finished=true;
            if(string.IsNullOrEmpty(failure)&&total>=60&&!HasRequiredWorkload(Time.realtimeSinceStartupAsDouble-started,evidence.activeSeconds,evidence.commands,evidence.rounds,evidence.pauseCycles))
                failure="Required gameplay workload did not complete.";
            if(string.IsNullOrEmpty(failure)&&(evidence.errors>0||evidence.exceptions>0))failure="Runtime errors occurred during the workload.";
            if(string.IsNullOrEmpty(failure)&&evidence.backgroundMode&&(offscreen==null||offscreen.RenderedFrames==0))failure="Background run did not render any frames.";
            UnityEngine.Application.logMessageReceived-=OnLog;
            frames?.Dispose();memory?.Dispose();process?.Dispose();
            evidence.elapsedSeconds=Time.realtimeSinceStartupAsDouble-started;
            evidence.profileElapsedSeconds=Math.Max(0,Math.Min(profile,evidence.elapsedSeconds-warmup));
            evidence.completed=string.IsNullOrEmpty(failure)&&evidence.elapsedSeconds>=total;evidence.failure=failure;
            evidence.completedUtc=DateTime.UtcNow.ToString("O");evidence.profileSamples=samples.Count;
            evidence.timingSamples=samples.Count(s=>s.cpuMs>0);
            evidence.gpuSamples=samples.Count(s=>s.gpuMs>0);evidence.renderedFrames=offscreen!=null?offscreen.RenderedFrames:0;
            evidence.offscreenRenderCpuMeanMs=Mean(samples.Select(s=>s.offscreenRenderCpuMs));
            evidence.frameMeanMs=Mean(samples.Select(s=>s.frameMs));evidence.frameP95Ms=P95(samples.Select(s=>s.frameMs));evidence.frameMaxMs=samples.Count==0?0:samples.Max(s=>s.frameMs);
            evidence.cpuMeanMs=Mean(samples.Where(s=>s.cpuMs>0).Select(s=>s.cpuMs));evidence.cpuP95Ms=P95(samples.Where(s=>s.cpuMs>0).Select(s=>s.cpuMs));
            evidence.gpuMeanMs=Mean(samples.Where(s=>s.gpuMs>0).Select(s=>s.gpuMs));evidence.gpuP95Ms=P95(samples.Where(s=>s.gpuMs>0).Select(s=>s.gpuMs));
            evidence.mainMeanMs=Mean(samples.Select(s=>s.mainMs));evidence.mainP95Ms=P95(samples.Select(s=>s.mainMs));
            evidence.renderMeanMs=Mean(samples.Select(s=>s.renderMs));evidence.presentWaitMeanMs=Mean(samples.Select(s=>s.presentMs));
            evidence.slowestFrames=samples.OrderByDescending(s=>s.frameMs).Take(20).ToArray();
            evidence.performanceTargetMet=evidence.completed&&!evidence.backgroundMode&&Debug.isDebugBuild&&evidence.width==1920&&evidence.height==1080
                &&evidence.frameTimingSupported&&evidence.timingSamples>=samples.Count*.95&&profile>=600&&evidence.profileElapsedSeconds>=600
                &&samples.Count>0&&evidence.frameP95Ms<=16.7&&evidence.focusLostSeconds<1&&evidence.errors==0&&evidence.exceptions==0;
            evidence.backgroundFrameTargetMet=evidence.backgroundMode&&evidence.completed&&evidence.renderedFrames>0&&evidence.width==1920&&evidence.height==1080&&evidence.quality=="PC"&&evidence.profileElapsedSeconds>=600
                &&evidence.frameTimingSupported&&evidence.timingSamples>=samples.Count*.95&&samples.Count>0&&evidence.frameP95Ms<=16.7;
            File.WriteAllText(Path.Combine(directory,"soak.json"),JsonUtility.ToJson(evidence,true));
            Debug.Log("A4_SOAK_FINISHED "+directory+" completed="+evidence.completed+" failure="+failure);
            if(evidence.backgroundMode&&!UnityEngine.Application.isEditor)UnityEngine.Application.Quit(evidence.completed?0:1);
        }
        private static double Mean(IEnumerable<double> values){var array=values.ToArray();return array.Length==0?0:array.Average();}
        private static bool HasRequiredWorkload(double elapsed,double active,int commands,int rounds,int pauses)
            =>elapsed>0&&active>=elapsed*.8&&commands>=4&&rounds>=1&&pauses>=1;
        private static double P95(IEnumerable<double> values){var array=values.OrderBy(v=>v).ToArray();return array.Length==0?0:array[(int)Math.Ceiling(array.Length*.95)-1];}
        private void OnDestroy(){if(initialized&&!finished)Finish("Probe destroyed before requested duration.");}
    }
}
#endif
