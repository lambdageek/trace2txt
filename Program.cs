using System;
using System.Text.RegularExpressions;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.EventPipe;

namespace Trace2txt;

public class Filter {
    public Regex? EventFilter {get; set;}
    
    public Filter() {}

    private bool Match (TraceEvent evt)
    {
        if (EventFilter == null)
            return true;
        return EventFilter.IsMatch (evt.EventName);
    }

    public void OnTraceEvent(TraceEvent evt)
    {
        if (!Match(evt))
            return;
        Console.WriteLine (evt.Dump());
    }

    public void OnUnhandledEvent(TraceEvent evt)
    {
        Console.WriteLine("WARNING: unhandled event {0}", evt.Dump());
    }

    public void OnThreadSample(ClrThreadSampleTraceData data)
    {
        if (!Match(data))
            return;
        Console.WriteLine("ThreadSample: {0}", data.Dump());
    }

    public void OnThreadStackWalk(ClrThreadStackWalkTraceData data)
    {
        if (!Match(data))
            return;
        Console.WriteLine("ThreadStackWalk: {0}", data.Dump());
    }

}

public class Program
{
    enum Mode {DumpAll, DumpSamples};

    public static int Main(string[] args)
    {
        const string eventParamPrefix = "--event=";
        Mode mode = Mode.DumpSamples;
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: trace2txt [--all] [--event=<regex>] <trace-file>");
            return 1;
        }
        string? fileName = null;
        Filter filter = new();
        foreach (var arg in args) {
            if (arg == "--all")
            {
                mode = Mode.DumpAll;
                continue;
            }
            if (arg.StartsWith(eventParamPrefix))
            {
                filter.EventFilter = new Regex(arg[eventParamPrefix.Length..]);
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
                SetupDumpAll(t, filter);
                break;
            case Mode.DumpSamples:
                SetupDumpSamples(t, filter);
                break;                
        }

        t.Process();
        return 0;
    }

    public static void SetupDumpAll(EventPipeEventSource t, Filter filter)
    {
        t.Clr.All += filter.OnTraceEvent;
        t.AllEvents += filter.OnTraceEvent;
        Microsoft.Diagnostics.Tracing.Parsers.Clr.ClrRundownTraceEventParser clrRundownParser = new(t);
        SampleProfilerTraceEventParser sampleProfilerParser = new(t);
        clrRundownParser.All += filter.OnTraceEvent;
        sampleProfilerParser.All += filter.OnTraceEvent;

    }

    public static void SetupDumpSamples(EventPipeEventSource t, Filter filter)
    {
        SampleProfilerTraceEventParser sampleProfilerParser = new(t);
        Microsoft.Diagnostics.Tracing.Parsers.Clr.ClrRundownTraceEventParser clrRundownParser = new(t);

        t.Clr.All += filter.OnTraceEvent;
        clrRundownParser.All += filter.OnTraceEvent;
        sampleProfilerParser.ThreadSample += filter.OnThreadSample;
        sampleProfilerParser.ThreadStackWalk += filter.OnThreadStackWalk;
        t.UnhandledEvents += filter.OnUnhandledEvent;
    }


}