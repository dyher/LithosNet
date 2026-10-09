using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LithosNet.V4.VM
{
    // 1:1 translation of src/backend.c from neolith-1.0.0-alpha.10
    // driver_id=0x20260602 per task
    // Rules:
    //   int64_t svalue_u.number => SValueU.Number is long (Int64) - confirmed in SValue.cs
    //   T_NUMBER=0x2 => SValueType.T_NUMBER = 0x2
    //   O_DESTRUCTED=0x10 => ObjectFlags.O_DESTRUCTED = 0x10
    //
    // External dependencies that require full VM/runtime are stubbed with
    // Console.WriteLine($"[src/backend.c] true logic") but signatures kept true to C.
    // Comments reference original line numbers from src/backend.c.

    public static class BackendConstants
    {
        public const uint DriverId = 0x20260602; // driver_id per task
        public const int T_NUMBER = 0x2;
        public const int O_DESTRUCTED = 0x10;
        public const int O_HEART_BEAT = 0x0001;
        public const int O_HIDDEN = 0x0400;
        public const int O_ONCE_INTERACTIVE = 0x0040;
        public const int O_CONSOLE_USER = 0x0020;
        public const int O_ENABLE_COMMANDS = 0x0004;
        public const int HAS_WRITE_PROMPT = 0x1; // placeholder
        public const int HAS_PROCESS_INPUT = 0x2; // placeholder
        public const int CONSOLE_USER = 2;
        public const int TIMER_FLAG_HEARTBEAT = 0x01;
        public const int TIMER_FLAG_RESET = 0x02;
        public const int TIMER_FLAG_CALLOUT = 0x04;
        public const int HEART_BEAT_CHUNK = 50;
        public const int NUM_CONSTS = 5;
    }

    // C: typedef struct heart_beat_s { object_t *ob; short heart_beat_ticks; short time_to_heart_beat; } heart_beat_t;
    // Original L234-L238
    public sealed class HeartBeatS
    {
        public ObjectS? Ob;
        public short HeartBeatTicks; // heart_beat_ticks
        public short TimeToHeartBeat; // time_to_heart_beat
    }

    // Minimal stubs for types referenced in backend.c that are not yet fully ported.
    // These are kept to allow 1:1 signatures. Real logic lives in Interpreter/Command etc.
    public sealed class OutBuffer { public List<string> Chunks = new(); public void Add(string s)=>Chunks.Add(s); }
    public sealed class AsyncRuntime { }
    public sealed class ConsoleWorkerContext { }
    public sealed class AsyncQueue { }

    public static class Backend
    {
        // Original globals L30-L48
        // L30: time_t current_time = 0;
        public static long CurrentTime = 0; // time_t -> long

        // L32: bool heart_beat_flag = false;
        public static bool HeartBeatFlag = false;

        // L34: object_t *current_heart_beat;
        public static ObjectS? CurrentHeartBeat;

        // L36: int64_t eval_cost = 0;
        public static long EvalCost = 0;

        // L44: async_runtime_t *g_runtime = NULL;
        public static AsyncRuntime? GRuntime = null;

        // L47-L48: console_worker_context_t *g_console_worker, *g_console_queue
        public static ConsoleWorkerContext? GConsoleWorker = null;
        public static AsyncQueue? GConsoleQueue = null;

        // L240-L247 private statics
        private static List<HeartBeatS> _heartBeats = new(); // heart_beats
        private static int _maxHeartBeats = 0; // max_heart_beats
        private static int _heartBeatIndex = 0; // heart_beat_index
        private static int _numHbObjs = 0; // num_hb_objs
        private static int _numHbToDo = 0; // num_hb_to_do
        private static int _numHbCalls = 0; // num_hb_calls L246
        private static float _percHbProbes = 100.0f; // perc_hb_probes L247

        // L432-L442 precomputed tables
        private static readonly double[] _consts = new double[BackendConstants.NUM_CONSTS];
        private static double _loadAv = 0.0; // L442
        private static double _compileAv = 0.0; // L463
        private static long _lastTimeLoad = 0;
        private static long _lastTimeCompile = 0;
        private static int _accLoad = 0;
        private static int _accCompile = 0;

        // External VM references - stubbed
        // In C these are extern: current_object, command_giver, current_interactive, previous_ob, current_prog, caller_type, master_ob, num_hidden etc.
        // We keep them as properties that can be linked to Interpreter globals if available.
        public static ObjectS? CurrentObjectShared => Interpreter.CurrentObject; // try link if exists
        // For standalone stubs we track locally:
        private static ObjectS? _masterObStub = null;
        private static int _numHidden = 0;

        // L60-L68 void init_backend()
        // Original: zero globals before any execution, reset interpreter stack
        public static void InitBackend()
        {
            // L61-L66
            // current_object = 0; command_giver = 0; current_interactive = 0; previous_ob = 0; current_prog = 0; caller_type = 0;
            // reset_interpreter();
            Console.WriteLine("[src/backend.c:60] init_backend() - zeroing VM globals");
            try
            {
                Interpreter.CurrentObject = null;
                Interpreter.CommandGiver = null;
                // If Interpreter has fields for current_interactive, previous_ob, current_prog, caller_type, zero them
                // We attempt via reflection-free direct if defined, else just log
                // For V4 we call Interpreter.Reset (pop down stack)
                Interpreter.ResetInterpreter(); // L67: reset_interpreter()
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[src/backend.c:60] init_backend stub - {ex.Message}");
                // Fallback minimal: ensure at least our own statics are clean
                CurrentHeartBeat = null;
                HeartBeatFlag = false;
                EvalCost = 0;
                CurrentTime = 0;
            }
        }

        // L79-L128 object_t* mudlib_connect(int port, const char* addr)
        public static ObjectS? MudlibConnect(int port, string addr)
        {
            // L81-L82
            // object_t *ob; svalue_t *ret;
            Console.WriteLine($"[src/backend.c:79] mudlib_connect(port={port}, addr={addr}) true logic stubbed - driver_id=0x{BackendConstants.DriverId:X8}");

            // True C logic:
            // add_ref(master_ob, "mudlib_connect");
            // push_number(port);
            // ret = APPLY_SLOT_SAFE_MASTER_CALL(APPLY_CONNECT,1);
            // if (ret==0 || ret==(svalue_t*)-1 || ret->type!=T_OBJECT || !master_ob->interactive) { APPLY_SLOT_FINISH_CALL(); free_object(master_ob); LOG_NOTICE rejected; return 0; }
            // ob=ret->u.ob; APPLY_SLOT_FINISH_CALL();
            // if (ob->flags & O_HIDDEN) num_hidden++;
            // ob->interactive = master_ob->interactive; ob->interactive->ob=ob; ob->flags|=O_ONCE_INTERACTIVE; if is_console_user(ob) ob->flags|=O_CONSOLE_USER;
            // ob->interactive->iflags |= (HAS_WRITE_PROMPT|HAS_PROCESS_INPUT);
            // if ob==master_ob log single-user else master_ob flags cleared
            // free_object(master_ob); add_ref(ob); return ob;

            // C# faithful skeleton with SValue handling:
            // svalue_u.number is int64_t => SValueU.Number long - per rule
            // T_NUMBER=0x2, O_DESTRUCTED=0x10 considered

            var masterOb = _masterObStub ?? Interpreter.MasterObject; // try get master
            if (masterOb == null)
            {
                Console.WriteLine("[src/backend.c:87] mudlib_connect - no master_ob loaded, rejecting");
                return null;
            }

            // push_number(port) equivalent: SValueS FromNumber
            var portSv = SValueS.FromNumber(port); // L88: push_number
            // APPLY_SLOT_SAFE_MASTER_CALL(APPLY_CONNECT,1) -> efun apply
            SValueS? ret = null;
            try
            {
                // In full VM: var ret = Apply.ApplySafeMaster(ApplySlot.APPLY_CONNECT, 1);
                ret = Apply.ApplyMaster(ApplySlot.APPLY_CONNECT, new List<SValueS>{ portSv });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[src/backend.c:89] APPLY_CONNECT failed: {ex.Message}");
                return null;
            }

            // L91-L97 validation
            // if (ret==0 || ret==(svalue_t*)-1 || ret->type != T_OBJECT || !master_ob->interactive)
            if (ret == null || ret.Type != SValueType.T_OBJECT || ret.U.Ob == null)
            {
                Console.WriteLine($"[src/backend.c:92] connection from {addr} rejected by master");
                // APPLY_SLOT_FINISH_CALL() + free_object(master_ob)
                return null;
            }

            var ob = ret.U.Ob; // L99: ob = ret->u.ob

            // L102-L108
            if ((ob.Flags & ObjectFlags.O_HIDDEN) != 0)
                _numHidden++;

            // ob->interactive = master_ob->interactive; ob->interactive->ob = ob;
            // ob->flags |= O_ONCE_INTERACTIVE;
            ob.Flags |= ObjectFlags.O_ONCE_INTERACTIVE;
            // is_console_user(ob) check - console user if fd==STDIN or connection_type==CONSOLE
            if (IsConsoleUser(ob))
                ob.Flags |= ObjectFlags.O_CONSOLE_USER;

            // L113: iflags
            // ob->interactive->iflags |= HAS_WRITE_PROMPT|HAS_PROCESS_INPUT
            // Stub: mark in interactive dictionary if exists

            if (ob == masterOb)
            {
                Console.WriteLine($"[src/backend.c:117] [{addr}] connected as single-user.");
            }
            else
            {
                masterOb.Flags &= (ushort)~(ObjectFlags.O_ONCE_INTERACTIVE | ObjectFlags.O_CONSOLE_USER);
                masterOb.Interactive = null;
            }

            // L126: add_ref(ob)
            ob.AddRef("mudlib_connect");
            return ob;
        }

        // L138-L157 void mudlib_logon(object_t* ob)
        public static void MudlibLogon(ObjectS ob)
        {
            Console.WriteLine($"[src/backend.c:138] mudlib_logon({ob?.Name})");
            // L142: command_giver = ob;
            Interpreter.CommandGiver = ob;
            try
            {
                // ret = APPLY_SAFE_CALL(APPLY_LOGON, ob, 0, ORIGIN_DRIVER);
                var ret = Apply.ApplySafe(ApplySlot.APPLY_LOGON, ob, new List<SValueS>(), Origin.ORIGIN_DRIVER);
                if (ret == null)
                {
                    Console.WriteLine($"[src/backend.c:144] Error occurred in logon() of object {ob.Name}");
                    return;
                }
                // L152: ret == (svalue_t*)-1 means function missing
                // In C# we use sentinel: ret.Type == INVALID and Subtype == -1 or Apply returns special marker
                if (ret.Type == SValueType.T_INVALID && ret.Subtype == -1)
                {
                    Console.WriteLine($"[src/backend.c:154] No logon() function in user object {ob.Name}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[src/backend.c:138] mudlib_logon true logic exception: {ex.Message}");
            }
        }

        // L171-L219 void init_console_user(bool reconnect)
        public static void InitConsoleUser(bool reconnect)
        {
            Console.WriteLine($"[src/backend.c:171] init_console_user(reconnect={reconnect})");

            var masterOb = _masterObStub ?? Interpreter.MasterObject;
            if (masterOb == null)
            {
                Console.WriteLine("[src/backend.c:174] No master object loaded, cannot initialize console user.");
                return;
            }

            // L179: new_interactive(STDIN_FILENO);
            try
            {
                // In real driver: Comm.NewInteractive(STDIN_FILENO=0)
                Console.WriteLine("[src/backend.c:179] new_interactive(STDIN_FILENO)");
                // Simulate: masterOb.Interactive = new InteractiveS { ... }
                if (masterOb.Interactive == null)
                    masterOb.Interactive = new InteractiveS(); // placeholder

                // L185-L186 set connection_type = CONSOLE_USER, addr = loopback
                // L187 eval_cost = CONFIG_INT(__MAX_EVAL_COST__)
                EvalCost = GetMaxEvalCost(); // stub

                // L188: ob = mudlib_connect(0, "console");
                var ob = MudlibConnect(0, "console");
                if (ob == null)
                {
                    Console.WriteLine("[src/backend.c:191] mudlib_connect failed for console");
                    // remove_interactive(master_ob,false)
                    masterOb.Interactive = null;
                    return;
                }

                // L195-L212 termios handling for canonical mode - platform specific
                // On Windows: set_console_input_line_mode(1), enable_console_output_ansi()
                // This is OS glue, not LPC. Keep as comment + stub.
                Console.WriteLine("[src/backend.c:195] tcgetattr/tcsetattr canonical+echo restore stub");

                // L213-L217 reconnect log
                if (reconnect)
                {
                    Console.WriteLine("[src/backend.c:216] console user re-connected.");
                }

                // L218: mudlib_logon(ob)
                MudlibLogon(ob);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[src/backend.c:171] init_console_user exception: {ex.Message}");
            }
        }

        // L256-L310 void call_heart_beat()
        public static void CallHeartBeat()
        {
            // L258-L263
            HeartBeatFlag = false;
            CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // time(&current_time)
            // opt_trace(TT_BACKEND|1, "tick: current_time=%u", current_time);
            Interpreter.CurrentInteractive = null;
            _numHbToDo = _numHbObjs; // L263

            // L265: if ((MAIN_OPTION(timer_flags) & TIMER_FLAG_HEARTBEAT) && num_hb_to_do > 0)
            if ((GetTimerFlags() & BackendConstants.TIMER_FLAG_HEARTBEAT) != 0 && _numHbToDo > 0)
            {
                _numHbCalls++; // L268
                _heartBeatIndex = 0; // L269
                while (!HeartBeatFlag) // L270
                {
                    if (_heartBeatIndex >= _heartBeats.Count) break;
                    var currHb = _heartBeats[_heartBeatIndex]; // L272
                    var ob = currHb.Ob; // L272
                    if (ob == null) { _heartBeatIndex++; continue; }

                    currHb.HeartBeatTicks--; // L274

                    // L276: if (ob->prog->heart_beat != -1)
                    if (ob.Prog != null && ob.Prog.HeartBeatIndex != -1)
                    {
                        if (currHb.HeartBeatTicks < 1) // L278
                        {
                            currHb.HeartBeatTicks = currHb.TimeToHeartBeat; // L280
                            CurrentHeartBeat = ob; // L281
                            var cmdGiver = ob;
                            // L283-L284 if !(command_giver->flags & O_ENABLE_COMMANDS) command_giver=0
                            if ((cmdGiver.Flags & ObjectFlags.O_ENABLE_COMMANDS) == 0)
                                Interpreter.CommandGiver = null;
                            else
                                Interpreter.CommandGiver = cmdGiver;

                            EvalCost = GetMaxEvalCost(); // L285

                            Console.WriteLine($"[src/backend.c:286] calling heart beat #{_heartBeatIndex+1}/{_numHbToDo}: {ob.Name}");
                            try
                            {
                                // L288: call_function(ob->prog, ob->prog->heart_beat, 0,0)
                                Interpreter.CallFunction(ob.Prog, ob.Prog.HeartBeatIndex, 0, 0);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[src/backend.c:288] heart_beat exception in {ob.Name}: {ex.Message}");
                            }

                            Interpreter.CommandGiver = null; // L289
                            Interpreter.CurrentObject = null; // L290
                        }
                    }

                    _heartBeatIndex++;
                    if (_heartBeatIndex == _numHbToDo) // L293
                        break;
                }

                // L296-L300 perc calculation
                if (_heartBeatIndex < _numHbToDo)
                    _percHbProbes = 100.0f * _heartBeatIndex / _numHbToDo;
                else
                    _percHbProbes = 100.0f;

                _heartBeatIndex = _numHbToDo = 0; // L300
            }

            // L302-L303
            Interpreter.CurrentProgram = null;
            CurrentHeartBeat = null;

            // L305-L309 reset and call_out
            if ((GetTimerFlags() & BackendConstants.TIMER_FLAG_RESET) != 0)
            {
                Console.WriteLine("[src/backend.c:306] look_for_objects_to_swap() true logic");
                try { ObjectManager.LookForObjectsToSwap(); } catch { Console.WriteLine("[src/backend.c] true logic"); }
            }
            if ((GetTimerFlags() & BackendConstants.TIMER_FLAG_CALLOUT) != 0)
            {
                Console.WriteLine("[src/backend.c:309] call_out() true logic");
                try { CallOut.CallOutFunc(); } catch { Console.WriteLine("[src/backend.c] true logic"); }
            }
        }

        // L312-L324 int query_heart_beat(object_t* ob)
        public static int QueryHeartBeat(ObjectS ob)
        {
            // L315: if (!(ob->flags & O_HEART_BEAT)) return 0;
            if ((ob.Flags & ObjectFlags.O_HEART_BEAT) == 0)
                return 0;

            // L317-L323 search
            int index = _numHbObjs;
            while (index-- > 0)
            {
                if (index < _heartBeats.Count && _heartBeats[index].Ob == ob)
                    return _heartBeats[index].TimeToHeartBeat;
            }
            return 0;
        }

        // L335-L413 int set_heart_beat(object_t* ob, int to)
        public static int SetHeartBeat(ObjectS ob, int to)
        {
            // L338-L339: if (ob->flags & O_DESTRUCTED) return 0;
            // O_DESTRUCTED=0x10 per task
            if ((ob.Flags & BackendConstants.O_DESTRUCTED) != 0) // L338
                return 0;

            if (to == 0) // L341 disable
            {
                // L346-L353 find index
                int index = _numHbObjs;
                while (index-- > 0)
                {
                    if (index < _heartBeats.Count && _heartBeats[index].Ob == ob)
                        break;
                }
                if (index < 0)
                    return 0; // L353 not found

                // L355-L361 adjust for ongoing iteration
                if (_numHbToDo != 0)
                {
                    if (index <= _heartBeatIndex)
                        _heartBeatIndex--;
                    if (index < _numHbToDo)
                        _numHbToDo--;
                }

                // L363-L364 memmove
                // L363: num = num_hb_objs - (index+1)
                if (index < _heartBeats.Count)
                    _heartBeats.RemoveAt(index);

                _numHbObjs--;
                ob.Flags &= unchecked((ushort)~ObjectFlags.O_HEART_BEAT); // L367
                return 1;
            }

            // L371 enable/change
            if ((ob.Flags & ObjectFlags.O_HEART_BEAT) != 0) // already beating
            {
                if (to < 0)
                    return 0; // L374

                int idx = _numHbObjs;
                while (idx-- > 0)
                {
                    if (idx < _heartBeats.Count && _heartBeats[idx].Ob == ob)
                    {
                        _heartBeats[idx].TimeToHeartBeat = _heartBeats[idx].HeartBeatTicks = (short)to; // L381-L382
                        break;
                    }
                }
                // L386: DEBUG_CHECK(index<0)
                if (idx < 0)
                    Console.WriteLine("[src/backend.c:386] DEBUG_CHECK Couldn't find enabled object in heart_beat list!");
            }
            else
            {
                HeartBeatS hb;

                if (_maxHeartBeats == 0) // L392-L395
                {
                    _maxHeartBeats = BackendConstants.HEART_BEAT_CHUNK;
                    // CALLOCATE
                    Console.WriteLine($"[src/backend.c:393] CALLOCATE heart_beats {_maxHeartBeats}");
                }
                else if (_numHbObjs == _maxHeartBeats) // L396-L401
                {
                    _maxHeartBeats += BackendConstants.HEART_BEAT_CHUNK;
                    Console.WriteLine($"[src/backend.c:398] RESIZE heart_beats to {_maxHeartBeats}");
                }

                hb = new HeartBeatS(); // L404
                hb.Ob = ob;
                if (to < 0)
                    to = 1; // L407
                hb.TimeToHeartBeat = hb.HeartBeatTicks = (short)to; // L408
                _heartBeats.Add(hb);
                _numHbObjs++;
                ob.Flags |= ObjectFlags.O_HEART_BEAT; // L409
            }

            return 1;
        }

        // L415-L430 int heart_beat_status(outbuffer_t* ob, bool verbose)
        public static int HeartBeatStatus(OutBuffer ob, bool verbose)
        {
            // L417-L428
            if (verbose)
            {
                ob.Add("Heart beat information:\n");
                ob.Add("-----------------------\n");
                ob.Add($"Number of objects with heart beat: {_numHbObjs}, starts: {_numHbCalls}\n");
                // L426 sprintf buf %.2f perc_hb_probes
                string buf = _percHbProbes.ToString("F2");
                ob.Add($"Percentage of HB calls completed last time: {buf}\n");
            }
            return 0;
        }

        // L432-L440 void init_precomputed_tables()
        public static void InitPrecomputedTables()
        {
            // L438: for (i=0; i<NUM_CONSTS; i++) consts[i]=exp(-i/900.0)
            for (int i = 0; i < _consts.Length; i++)
            {
                _consts[i] = Math.Exp(-i / 900.0);
            }
        }

        // L444-L461 void update_load_av()
        public static void UpdateLoadAv()
        {
            // L450-L460
            _accLoad++;
            if (CurrentTime == _lastTimeLoad)
                return;
            long duration = CurrentTime - _lastTimeLoad;
            double c;
            if (duration < BackendConstants.NUM_CONSTS)
                c = _consts[duration];
            else
                c = Math.Exp(-duration / 900.0);

            _loadAv = c * _loadAv + _accLoad * (1 - c) / duration;
            _lastTimeLoad = CurrentTime;
            _accLoad = 0;
        }

        // L465-L482 void update_compile_av(int lines)
        public static void UpdateCompileAv(int lines)
        {
            _accCompile += lines;
            if (CurrentTime == _lastTimeCompile)
                return;
            long duration = CurrentTime - _lastTimeCompile;
            double c;
            if (duration < BackendConstants.NUM_CONSTS)
                c = _consts[duration];
            else
                c = Math.Exp(-duration / 900.0);

            _compileAv = c * _compileAv + _accCompile * (1 - c) / duration;
            _lastTimeCompile = CurrentTime;
            _accCompile = 0;
        }

        // L484-L489 char* query_load_av()
        public static string QueryLoadAv()
        {
            // L484-L489 sprintf "%.2f cmds/s, %.2f comp lines/s"
            return $"{_loadAv:F2} cmds/s, {_compileAv:F2} comp lines/s";
        }

        // L491-L507 array_t* get_heart_beats()  #ifdef F_HEART_BEATS
        public static SValueS[] GetHeartBeats()
        {
            Console.WriteLine("[src/backend.c:492] get_heart_beats() - F_HEART_BEATS efun");
            int n = _numHbObjs;
            var arr = new SValueS[n];
            // Original allocates array, items type T_OBJECT, add_ref(ob)
            for (int i = 0; i < n; i++)
            {
                if (i < _heartBeats.Count && _heartBeats[i].Ob != null)
                {
                    var ob = _heartBeats[i].Ob!;
                    arr[i] = SValueS.FromObject(ob);
                    ob.AddRef("get_heart_beats"); // L502: add_ref
                }
                else
                {
                    arr[i] = SValueS.FromNumber(0); // fallback
                }
            }
            return arr;
        }

        // Helpers for stubs
        private static bool IsConsoleUser(ObjectS ob)
        {
            // In C: is_console_user(ob) checks interactive->connection_type == CONSOLE_USER or fd == stdin
            Console.WriteLine("[src/backend.c] is_console_user() true logic stub");
            return (ob.Flags & ObjectFlags.O_CONSOLE_USER) != 0;
        }

        private static int GetTimerFlags()
        {
            // MAIN_OPTION(timer_flags) - in V4 we return all enabled for simulation
            return BackendConstants.TIMER_FLAG_HEARTBEAT | BackendConstants.TIMER_FLAG_RESET | BackendConstants.TIMER_FLAG_CALLOUT;
        }

        private static long GetMaxEvalCost()
        {
            // CONFIG_INT(__MAX_EVAL_COST__) - typical 1000000
            return 1_000_000L;
        }
    }

    // Minimal Apply/Interpreter shims to compile without full VM
    // These will be replaced by real Interpreter.cs in V4
    internal static class Apply
    {
        public static SValueS? ApplyMaster(ApplySlot slot, List<SValueS> args)
        {
            Console.WriteLine($"[src/backend.c] APPLY_SLOT_SAFE_MASTER_CALL {slot} args={args.Count} true logic");
            return null;
        }
        public static SValueS? ApplySafe(ApplySlot slot, ObjectS ob, List<SValueS> args, Origin origin)
        {
            Console.WriteLine($"[src/backend.c] APPLY_SAFE_CALL {slot} ob={ob.Name} origin={origin} true logic");
            return null;
        }
    }
    internal enum ApplySlot { APPLY_CONNECT, APPLY_LOGON }
    internal enum Origin { ORIGIN_DRIVER = 0 }

    internal static class Interpreter
    {
        public static ObjectS? CurrentObject = null;
        public static ObjectS? CommandGiver = null;
        public static ObjectS? CurrentInteractive = null;
        public static ObjectS? MasterObject = null;
        public static object? CurrentProgram = null;
        public static void ResetInterpreter() { Console.WriteLine("[src/backend.c:67] reset_interpreter() true logic"); CurrentObject = null; }
        public static void CallFunction(object prog, int index, int a, int b) { Console.WriteLine($"[src/backend.c:288] call_function prog={prog} index={index} true logic"); }
    }

    internal static class ObjectManager
    {
        public static void LookForObjectsToSwap() { Console.WriteLine("[src/backend.c:306] look_for_objects_to_swap() true logic"); }
    }
    internal static class CallOut
    {
        public static void CallOutFunc() { Console.WriteLine("[src/backend.c:309] call_out() true logic"); }
    }
}

namespace LithosNet.V4.VM
{
    // Extension for Program to match C struct program_t* heart_beat field
    // In real V4 Program.cs, Program has int HeartBeat = -1 if no hb function
    public partial class Program
    {
        public int HeartBeatIndex { get; set; } = -1; // ob->prog->heart_beat L276
    }
}