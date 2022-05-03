using System;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.EventPipe;

namespace Trace2txt;

public class Program
{
    enum Mode {DumpAll, DumpSamples};

    public static int Main(string[] args)
    {
        Mode mode = Mode.DumpSamples;
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: trace2txt [--all] <trace-file>");
            return 1;
        }
        string? fileName = null;
        foreach (var arg in args) {
            if (arg == "--all")
            {
                mode = Mode.DumpAll;
                continue;
            }
            if (fileName == null)
                fileName = arg;
            else {
                Console.Error.WriteLine ("Too many file name arguments");
                return 1;
            }
            
        }
        if (fileName == null) {
            Console.Error.WriteLine ("No file name argument");
            return 1;
        }
        using var t = new EventPipeEventSource(fileName);
        if (t.EventsLost != 0)
        {
            Console.Error.WriteLine("WARNING: there were {0} lost events", t.EventsLost);
        }


        switch (mode) {
            case Mode.DumpAll:
                SetupDumpAll(t);
                break;
            case Mode.DumpSamples:
                SetupDumpSamples(t);
                break;                
        }

        t.Process();
        return 0;
    }

    public static void SetupDumpAll(EventPipeEventSource t)
    {
        t.Clr.All += OnTraceEvent;
        t.AllEvents += OnTraceEvent;
        Microsoft.Diagnostics.Tracing.Parsers.Clr.ClrRundownTraceEventParser clrRundownParser = new(t);
        SampleProfilerTraceEventParser sampleProfilerParser = new(t);
        clrRundownParser.All += OnTraceEvent;
        sampleProfilerParser.All += OnTraceEvent;

    }

    public static void SetupDumpSamples(EventPipeEventSource t)
    {
        SampleProfilerTraceEventParser sampleProfilerParser = new(t);
        Microsoft.Diagnostics.Tracing.Parsers.Clr.ClrRundownTraceEventParser clrRundownParser = new(t);

        t.Clr.All += OnTraceEvent;
        clrRundownParser.All += OnTraceEvent;
        sampleProfilerParser.ThreadSample += OnThreadSample;
        sampleProfilerParser.ThreadStackWalk += OnThreadStackWalk;
        t.UnhandledEvents += OnUnhandledEvent;
    }

    public static void OnTraceEvent(TraceEvent evt)
    {
        Console.WriteLine (evt.Dump());
    }

    public static void OnUnhandledEvent(TraceEvent evt)
    {
        Console.WriteLine("WARNING: unhandled event {0}", evt.Dump());
    }

    public static void OnThreadSample(ClrThreadSampleTraceData data)
    {
        Console.WriteLine("ThreadSample: {0}", data.Dump());
    }

    public static void OnThreadStackWalk(ClrThreadStackWalkTraceData data)
    {
        Console.WriteLine("ThreadStackWalk: {0}", data.Dump());
    }

}