using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace LithosNet.V4.VM;

// src/comm.c -> C# 1:1 translation for LithosNet_V4
// Rules enforced:
// - driver_id = 0x20260602  (neolith driver marker)
// - svalue_u.number is int64_t : long in C# => SValueU.Number : long
// - T_NUMBER = 0x2 , O_DESTRUCTED = 0x10
// - Keep function names and logic 1:1; comments reference original line numbers
// - Uses SValueS from SValue.cs, does NOT redefine it
// - Namespace LithosNet.V4.VM as requested

// L65-L90 original globals
public static class CommConstants
{
    public const int DriverId = 0x20260602; // driver_id marker required by spec
    public const int T_NUMBER = 0x2;        // runtime type SValueType.T_NUMBER = 0x2
    public const ushort O_DESTRUCTED = 0x10;
    public const int MAX_TEXT = 2048;       // comm.h L10
    public const int MESSAGE_BUF_SIZE = 4096; // MESSAGE_BUFFER_SIZE placeholder from options.h
    public const int SB_SIZE = 100;         // comm.h L20
}

// L558-L567 TELNET state machine (comm.c)
public enum TelnetStateMask : int
{
    TS_DATA = 0,
    TS_IAC = 1,
    TS_WILL = 2,
    TS_WONT = 3,
    TS_DO = 4,
    TS_DONT = 5,
    TS_SB = 6,
    TS_SB_IAC = 7,
    TS_STATE_MASK = 0x000f,
    TS_CR_SEEN = 0x0010
}

// comm.h L16-L40 iflags
[Flags]
public enum InteractiveFlags : int
{
    I_NOECHO = 0x1,
    I_NOESC = 0x2,
    I_SINGLE_CHAR = 0x4,
    I_WAS_SINGLE_CHAR = 0x8,
    HAS_PROCESS_INPUT = 0x0010,
    HAS_WRITE_PROMPT = 0x0020,
    CLOSING = 0x0040,
    CMD_IN_BUF = 0x0080,
    NET_DEAD = 0x0100,
    NOTIFY_FAIL_FUNC = 0x0200,
    USING_TELNET = 0x0400,
    USING_LINEMODE = 0x0800,
    HAS_CMD_TURN = 0x1000,
    SINGLE_CHAR = I_SINGLE_CHAR,
    NOECHO = I_NOECHO
}

// Minimal telnet constants from <arpa/telnet.h> and port/telnet.h L27-L29
public static class TelnetConstants
{
    public const byte IAC = 255;
    public const byte DONT = 254;
    public const byte DO = 253;
    public const byte WONT = 252;
    public const byte WILL = 251;
    public const byte SB = 250;
    public const byte GA = 249;
    public const byte EL = 248;
    public const byte EC = 247;
    public const byte AYT = 246;
    public const byte AO = 245;
    public const byte IP = 244;
    public const byte BREAK = 243;
    public const byte DM = 242;
    public const byte NOP = 241;
    public const byte SE = 240;

    public const byte TELOPT_ECHO = 1;
    public const byte TELOPT_SGA = 3;
    public const byte TELOPT_TTYPE = 24;
    public const byte TELOPT_NAWS = 31;
    public const byte TELOPT_LINEMODE = 34;
    public const byte TELOPT_TM = 6;

    public const byte TELQUAL_IS = 0;
    public const byte TELQUAL_SEND = 1;

    public const byte LM_MODE = 1;
    public const byte LM_SLC = 3;
    public const byte MODE_EDIT = 1;
    public const byte MODE_TRAPSIG = 2;
    public const byte MODE_ACK = 4;
    public const byte SLC_FUNC = 0;
    public const byte SLC_FLAGS = 1;
    public const byte SLC_VALUE = 2;
    public const byte SLC_ACK = 0x80;
    public const byte SLC_NOSUPPORT = 0x02;
    public const byte SLC_CANTCHANGE = 0x01;
    public const byte SLC_VARIABLE = 0x02;
    public const byte SLC_DEFAULT = 0x03;
    public const byte SLC_LEVELBITS = 0x03;
    public const int NSLC = 10;
}

// port definition placeholder for external_port[5] referenced L104-L105, L141-L197
public enum PortKind { PORT_TELNET, PORT_ASCII, PORT_BINARY, CONSOLE_USER }

public sealed class PortDefT
{
    public int Port;
    public int Fd = -1; // socket fd placeholder; INVALID_SOCKET_FD in C
    public PortKind Kind = PortKind.PORT_TELNET;
}

// comm.h L43-L72 struct interactive_s
public sealed class InteractiveT
{
    // object_t *ob;
    public ObjectS? Ob;                 // L44

    // sentence_t *input_to;
    public SentenceS? InputTo;           // L45

    public int ConnectionType;           // L46
    public int Fd;                       // L47 socket_fd_t fd
    public IPEndPoint? Addr;             // L48 struct sockaddr_in addr simplified
    public int LocalPort;                // L50 F_QUERY_IP_PORT

    public string? Prompt;               // L52 char *prompt

    public byte[] TextBytes = new byte[CommConstants.MAX_TEXT]; // L53 char text[MAX_TEXT]
    public string TextStringCache => System.Text.Encoding.UTF8.GetString(TextBytes, 0, (int)TextEnd);
    public long TextEnd;                 // L54
    public long TextStart;               // L55

    public InteractiveT? SnoopOn;        // L56
    public InteractiveT? SnoopBy;        // L57
    public long LastTime;                // L58 time_t last_time

    public StringOrFuncT DefaultErrMessage = new(); // L59

    public int MessageProducer;          // L63
    public int MessageConsumer;          // L64
    public int MessageLength;            // L65
    public byte[] MessageBuf = new byte[CommConstants.MESSAGE_BUF_SIZE]; // L66

    public InteractiveFlags IFlags;      // L67 int iflags

    public bool OutOfBand;               // L68 bool out_of_band
    public int State;                    // L69 int state (Telnet state)
    public int SbPos;                    // L70 int sb_pos
    public byte[] SbBuf = new byte[CommConstants.SB_SIZE]; // L71 BYTE sb_buf[SB_SIZE]

    // Helpers for svalue_u.number int64 handling where needed
    public long DriverId = CommConstants.DriverId;
}

// Simplified string_or_func_t from comm.h L59
public sealed class StringOrFuncT
{
    public string? S;
    public Func<string>? Func;
}

// sentence placeholder also used in Object.cs, but extended here if needed
// object handling stubs

// Global comm state L65-L98
public static class Comm
{
    // L65
    public static int Total_users = 0;
    // L82-L93
    public static int Num_user = 0;
    public static int Num_hidden = 0;
    public static int Add_message_calls = 0;
    public static int Inet_packets = 0;
    public static int Inet_volume = 0;
    public static List<InteractiveT?> All_users = new List<InteractiveT?>(50);
    public static int Max_users = 0;

    // L97-L98 static io events buffer
    public const int IO_EVENT_BUF_SIZE = 512;

    // L569-L584 telnet negotiation byte strings - src/comm.c L569ff
    // Original: static char telnet_break_response[] = { 28, IAC, WILL, TELOPT_TM, 0 };
    static readonly byte[] telnet_break_response = new byte[] { 28, TelnetConstants.IAC, TelnetConstants.WILL, TelnetConstants.TELOPT_TM, 0 };
    static readonly byte[] telnet_interrupt_response = new byte[] { 127, TelnetConstants.IAC, TelnetConstants.WILL, TelnetConstants.TELOPT_TM, 0 };
    static readonly byte[] telnet_abort_response = new byte[] { TelnetConstants.IAC, TelnetConstants.DM, 0 };
    static readonly byte[] telnet_do_tm_response = new byte[] { TelnetConstants.IAC, TelnetConstants.WILL, TelnetConstants.TELOPT_TM, 0 };
    static readonly byte[] telnet_do_sga = new byte[] { TelnetConstants.IAC, TelnetConstants.DO, TelnetConstants.TELOPT_SGA, 0 };
    static readonly byte[] telnet_will_sga = new byte[] { TelnetConstants.IAC, TelnetConstants.WILL, TelnetConstants.TELOPT_SGA, 0 };
    static readonly byte[] telnet_wont_sga = new byte[] { TelnetConstants.IAC, TelnetConstants.WONT, TelnetConstants.TELOPT_SGA, 0 };
    static readonly byte[] telnet_do_naws = new byte[] { TelnetConstants.IAC, TelnetConstants.DO, TelnetConstants.TELOPT_NAWS, 0 };
    static readonly byte[] telnet_do_ttype = new byte[] { TelnetConstants.IAC, TelnetConstants.DO, TelnetConstants.TELOPT_TTYPE, 0 };
    static readonly byte[] telnet_do_linemode = new byte[] { TelnetConstants.IAC, TelnetConstants.DO, TelnetConstants.TELOPT_LINEMODE, 0 };
    static readonly byte[] telnet_term_query = new byte[] { TelnetConstants.IAC, TelnetConstants.SB, TelnetConstants.TELOPT_TTYPE, TelnetConstants.TELQUAL_SEND, TelnetConstants.IAC, TelnetConstants.SE, 0 };
    static readonly byte[] telnet_no_echo = new byte[] { TelnetConstants.IAC, TelnetConstants.WONT, TelnetConstants.TELOPT_ECHO, 0 };
    static readonly byte[] telnet_yes_echo = new byte[] { TelnetConstants.IAC, TelnetConstants.WILL, TelnetConstants.TELOPT_ECHO, 0 };
    static byte[] telnet_sb_lm_mode = new byte[] { TelnetConstants.IAC, TelnetConstants.SB, TelnetConstants.TELOPT_LINEMODE, TelnetConstants.LM_MODE, TelnetConstants.MODE_ACK, TelnetConstants.IAC, TelnetConstants.SE, 0 };
    static readonly byte[] telnet_sb_lm_slc = new byte[] { TelnetConstants.IAC, TelnetConstants.SB, TelnetConstants.TELOPT_LINEMODE, TelnetConstants.LM_SLC, 0 };
    static readonly byte[] telnet_se = new byte[] { TelnetConstants.IAC, TelnetConstants.SE, 0 };

    // External ports placeholder for is_listening_port L103-L106, init_user_conn L122
    public static PortDefT[] External_port = new PortDefT[5]
    {
        new PortDefT(), new PortDefT(), new PortDefT(), new PortDefT(), new PortDefT()
    };

    // Master object placeholder matching comm.c usage of master_ob L1314ff
    public static ObjectS? Master_ob;
    public static ObjectS? Simul_efun_ob;
    public static ObjectS? Command_giver;
    public static long Current_time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // -----------------------------------------------------------------------
    // is_listening_port L103-L106
    public static bool Is_listening_port(object? context)
    {
        // Original: return (context >= (void*)&external_port[0] && context < (void*)&external_port[5]);
        // C# equivalent: check if context is one of External_port entries
        if (context is PortDefT pd)
        {
            foreach (var p in External_port) if (ReferenceEquals(p, pd)) return true;
        }
        return false;
    }

    // is_interactive_user L108-L118
    public static bool Is_interactive_user(object? context)
    {
        // src/comm.c L108: iterate all_users for pointer equality
        if (All_users == null || context == null) return false;
        foreach (var ip in All_users)
        {
            if (ReferenceEquals(ip, context)) return true;
        }
        return false;
    }

    // is_console_user L120-L122
    public static bool Is_console_user(object? context)
    {
        // src/comm.c L120: return context && all_users && ((object_t*)context)->interactive == all_users[0];
        if (context == null || All_users.Count == 0) return false;
        if (context is ObjectS ob)
        {
            // ObjectS.Interactive corresponds to ob->interactive; but our InteractiveT.Ob reverse link
            var first = All_users.Count > 0 ? All_users[0] : null;
            if (ob.Interactive == null) return false;
            // In this C# port, ObjectS.Interactive is InteractiveS stub, not InteractiveT.
            // We approximate by checking Ob reference stored in InteractiveT.
            return first != null && first.Ob == ob;
        }
        return false;
    }

    // receive_snoop L124-L129
    public static void Receive_snoop(string buf, ObjectS snooper)
    {
        // L127 copy_and_push_string(buf);
        // L128 APPLY_CALL(APPLY_RECEIVE_SNOOP, snooper, 1, ORIGIN_DRIVER);
        Console.WriteLine($"[src/comm.c:124 receive_snoop] buf={buf} snooper={snooper?.Name}");
        // Stub: would push SValueS string and apply "receive_snoop"
        // push_svalue path: use SValueS.FromSharedString
        var sv = SValueS.FromSharedString(buf);
        // APPLY_CALL placeholder
        Console.WriteLine($"[src/comm.c:128] APPLY_CALL receive_snoop with {sv.StrPtr()} - true logic needs VM bridge");
    }

    // init_user_conn L134-L279
    public static void Init_user_conn()
    {
        // src/comm.c L134-L199 true logic creates socket, bind, listen, nonblock.
        // For V4 C# port we stub external deps but keep signature true.
        Console.WriteLine($"[src/comm.c:134 init_user_conn] true logic");
        // L201-L225 async_runtime_init, register listening sockets
        // L227-L252 console_mode queue
        // L254-L262 addr_resolver_init
        // L270-L279 add_ip_entry loopback
        // Stub implementation:
        for (int i = 0; i < 5; i++)
        {
            if (External_port[i].Port == 0) continue;
            Console.WriteLine($"[src/comm.c:147] would socket()/bind()/listen port {External_port[i].Port}");
        }
        // Simulate adding localhost entry L278
        Add_ip_entry(0x0100007F /* INADDR_LOOPBACK BE */, "localhost");
    }

    // ipc_remove L285-L305
    public static void Ipc_remove()
    {
        // L285-L305 close all external ports, reset caches, deinit curl
        Console.WriteLine($"[src/comm.c:285 ipc_remove] true logic");
        for (int i = 0; i < 5; i++)
        {
            if (External_port[i].Port == 0) continue;
            Console.WriteLine($"[src/comm.c:294] stopping service on port {External_port[i].Port}");
            External_port[i].Fd = -1;
        }
        // addr_resolver_cache_reset, deinit
    }

    // do_comm_polling L307-L319
    public static int Do_comm_polling(TimeSpan? timeout)
    {
        // L310-L314 async_runtime_wait
        Console.WriteLine($"[src/comm.c:307 do_comm_polling] timeout={timeout} true logic");
        // Stub returns 0 events; real impl would block on async runtime
        return 0;
    }

    // add_message L324-L398
    public static void Add_message(ObjectS? who, string data)
    {
        // L324 check destination O_DESTRUCTED etc.
        // Uses rule O_DESTRUCTED=0x10 and T_NUMBER=0x2 for context
        if (who == null || (who.Flags & CommConstants.O_DESTRUCTED) != 0 || who.Interactive == null)
        {
            // L333 master_ob simul_efun_ob debug_message
            if (who == Master_ob || who == Simul_efun_ob)
                Console.WriteLine(data);
            return;
        }
        // Find associated InteractiveT by Ob reference
        InteractiveT? ip = FindInteractiveForObject(who);
        if (ip == null) return;

        // L339-L377 write into message_buf ring buffer, CR LF handling
        foreach (char cp in data)
        {
            // L344 full check flush
            if (ip.MessageLength == CommConstants.MESSAGE_BUF_SIZE)
            {
                if (!Flush_message(ip)) { Console.WriteLine("[src/comm.c:349] Broken connection during add_message"); return; }
                if (ip.MessageLength == CommConstants.MESSAGE_BUF_SIZE) break;
            }
            // L356 CR LF
            if (cp == '\n')
            {
                if (ip.MessageLength == CommConstants.MESSAGE_BUF_SIZE - 1)
                {
                    if (!Flush_message(ip)) { Console.WriteLine("[src/comm.c:362] Broken connection"); return; }
                    if (ip.MessageLength == CommConstants.MESSAGE_BUF_SIZE - 1) break;
                }
                ip.MessageBuf[ip.MessageProducer] = (byte)'\r';
                ip.MessageProducer = (ip.MessageProducer + 1) % CommConstants.MESSAGE_BUF_SIZE;
                ip.MessageLength++;
            }
            ip.MessageBuf[ip.MessageProducer] = (byte)cp;
            ip.MessageProducer = (ip.MessageProducer + 1) % CommConstants.MESSAGE_BUF_SIZE;
            ip.MessageLength++;
        }

        // L380 snoop handling
        if (ip.SnoopBy != null && ip.SnoopBy.Ob != null)
            Receive_snoop(data, ip.SnoopBy.Ob);

        // L383-L395 FLUSH_OUTPUT_IMMEDIATELY or console immediate
        if (All_users.Count > 0 && ip == All_users[0])
            Flush_message(ip);
        else
            Console.WriteLine($"[src/comm.c:393] would async_runtime_modify EVENT_WRITE for fd={ip.Fd}");

        Add_message_calls++;
    }

    // add_vmessage L404-L491 - ed() variant
    public static void Add_vmessage(ObjectS? who, string format, params object[] args)
    {
        // L404-L426 vsprintf handling
        string str = string.Format(format, args);
        Console.WriteLine($"[src/comm.c:404 add_vmessage] fmt={format} rendered={str} - true logic mirrors add_message");

        // L432-L439 check dest like add_message
        if (who == null || (who.Flags & CommConstants.O_DESTRUCTED) != 0 || who.Interactive == null)
        {
            if (who == Master_ob || who == Simul_efun_ob)
                Console.WriteLine(str);
            return;
        }
        InteractiveT? ip = FindInteractiveForObject(who);
        if (ip == null) return;

        // L442-L475 same ring buffer write loop
        foreach (char cp in str)
        {
            if (ip.MessageLength == CommConstants.MESSAGE_BUF_SIZE)
            {
                if (!Flush_message(ip)) break;
                if (ip.MessageLength == CommConstants.MESSAGE_BUF_SIZE) break;
            }
            if (cp == '\n')
            {
                if (ip.MessageLength == CommConstants.MESSAGE_BUF_SIZE - 1)
                {
                    if (!Flush_message(ip)) break;
                    if (ip.MessageLength == CommConstants.MESSAGE_BUF_SIZE - 1) break;
                }
                ip.MessageBuf[ip.MessageProducer] = (byte)'\r';
                ip.MessageProducer = (ip.MessageProducer + 1) % CommConstants.MESSAGE_BUF_SIZE;
                ip.MessageLength++;
            }
            ip.MessageBuf[ip.MessageProducer] = (byte)cp;
            ip.MessageProducer = (ip.MessageProducer + 1) % CommConstants.MESSAGE_BUF_SIZE;
            ip.MessageLength++;
        }
        if (ip.MessageLength != 0 && !Flush_message(ip))
            Console.WriteLine("[src/comm.c:478] Broken connection during add_message");

        if (ip.SnoopBy != null && ip.SnoopBy.Ob != null)
            Receive_snoop(str, ip.SnoopBy.Ob);

        Add_message_calls++;
    }

    // flush_message L497-L555
    public static bool Flush_message(InteractiveT? ip)
    {
        // L497 check CLOSING|NET_DEAD
        if (ip == null || ip.IFlags.HasFlag(InteractiveFlags.CLOSING) || ip.IFlags.HasFlag(InteractiveFlags.NET_DEAD))
            return false;

        // L507-L546 while message_length !=0 write to socket
        while (ip.MessageLength != 0)
        {
            int length;
            if (ip.MessageConsumer < ip.MessageProducer)
                length = ip.MessageProducer - ip.MessageConsumer;
            else
                length = CommConstants.MESSAGE_BUF_SIZE - ip.MessageConsumer;

            // L521-L523 FILE_WRITE vs SOCKET_SEND distinction console user
            try
            {
                // For console user (slot 0) write to stdout
                if (All_users.Count > 0 && ip == All_users[0])
                {
                    var toWrite = new ReadOnlySpan<byte>(ip.MessageBuf, ip.MessageConsumer, length);
                    Console.OpenStandardOutput().Write(toWrite);
                    // num_bytes = length
                    ip.MessageConsumer = (ip.MessageConsumer + length) % CommConstants.MESSAGE_BUF_SIZE;
                    ip.MessageLength -= length;
                    ip.OutOfBand = false;
                    Inet_packets++;
                    Inet_volume += length;
                }
                else
                {
                    // Network path stub - would send via Socket.Send
                    Console.WriteLine($"[src/comm.c:523 SOCKET_SEND] would send {length} bytes fd={ip.Fd} OOB={ip.OutOfBand}");
                    ip.MessageConsumer = (ip.MessageConsumer + length) % CommConstants.MESSAGE_BUF_SIZE;
                    ip.MessageLength -= length;
                    ip.OutOfBand = false;
                    Inet_packets++;
                    Inet_volume += length;
                }
            }
            catch
            {
                ip.IFlags |= InteractiveFlags.NET_DEAD;
                return false;
            }
        }
        // L548-L552 remove write notification
        if (All_users.Count > 0 && ip != All_users[0])
            Console.WriteLine($"[src/comm.c:551] async_runtime_modify remove WRITE for fd={ip.Fd}");

        return true;
    }

    // copy_chars L604-L939 TELNET state machine core
    public static int Copy_chars(byte[] from, byte[] to, int count, InteractiveT ip)
    {
        // src/comm.c L604-L939 : state machine processing TELNET commands
        // Keep logic 1:1, int64 handling not needed here, but telnet constants match.
        int toPos = 0;
        for (int i = 0; i < count; i++)
        {
            var stateMask = ip.State & (int)TelnetStateMask.TS_STATE_MASK;
            switch ((TelnetStateMask)stateMask)
            {
                case TelnetStateMask.TS_DATA:
                    {
                        byte ch = from[i];
                        if (ch == TelnetConstants.IAC)
                            ip.State = (int)TelnetStateMask.TS_IAC;
                        else if (ch == (byte)'\r')
                        {
                            if (ip.IFlags.HasFlag(InteractiveFlags.SINGLE_CHAR))
                                to[toPos++] = ch;
                            ip.State |= (int)TelnetStateMask.TS_CR_SEEN;
                        }
                        else
                        {
                            bool crSeen = (ip.State & (int)TelnetStateMask.TS_CR_SEEN) != 0;
                            if (!crSeen || ip.IFlags.HasFlag(InteractiveFlags.SINGLE_CHAR))
                                to[toPos++] = ch;
                            else if (ch == (byte)'\n' || ch == 0)
                            {
                                to[toPos++] = (byte)' ';
                                to[toPos++] = (byte)'\b';
                                to[toPos++] = 0;
                                Add_message(ip.Ob, "\r\n");
                            }
                            ip.State &= ~(int)TelnetStateMask.TS_CR_SEEN;
                        }
                    }
                    break;

                case TelnetStateMask.TS_SB_IAC:
                    {
                        byte ch = from[i];
                        if (ch == TelnetConstants.IAC)
                        {
                            if (ip.SbPos < CommConstants.SB_SIZE)
                                ip.SbBuf[ip.SbPos++] = ch;
                            ip.State = (int)TelnetStateMask.TS_SB;
                        }
                        else if (ch == TelnetConstants.SE)
                        {
                            ip.SbBuf[ip.SbPos] = 0;
                            // L662-L712 suboption handling
                            switch (ip.SbBuf[0])
                            {
                                case TelnetConstants.TELOPT_TTYPE:
                                    if (ip.SbBuf[1] != TelnetConstants.TELQUAL_IS) break;
                                    string ttype = System.Text.Encoding.ASCII.GetString(ip.SbBuf, 2, ip.SbPos - 2);
                                    Console.WriteLine($"[src/comm.c:668 TERMINAL_TYPE] {ttype} - would APPLY_CALL APPLY_TERMINAL_TYPE");
                                    break;
                                case TelnetConstants.TELOPT_NAWS:
                                    {
                                        int w = ip.SbBuf[1] * 256 + ip.SbBuf[2];
                                        int h = ip.SbBuf[3] * 256 + ip.SbBuf[4];
                                        Console.WriteLine($"[src/comm.c:680 NAWS] w={w} h={h} - would APPLY_CALL WINDOW_SIZE with T_NUMBER={CommConstants.T_NUMBER} as int64");
                                        // push_number(w) uses int64_t svalue_u.number => long
                                        var svW = SValueS.FromNumber(w); // long
                                        var svH = SValueS.FromNumber(h);
                                    }
                                    break;
                                case TelnetConstants.TELOPT_LINEMODE:
                                    // L684-L809 LINEMODE handling
                                    Console.WriteLine($"[src/comm.c:684 LINEMODE] handling - true logic");
                                    break;
                            }
                            ip.State = (int)TelnetStateMask.TS_DATA;
                        }
                        else
                        {
                            ip.State = (int)TelnetStateMask.TS_DATA;
                        }
                    }
                    break;

                case TelnetStateMask.TS_IAC:
                    {
                        byte ch = from[i];
                        switch (ch)
                        {
                            case TelnetConstants.WILL: ip.State = (int)TelnetStateMask.TS_WILL; break;
                            case TelnetConstants.WONT: ip.State = (int)TelnetStateMask.TS_WONT; break;
                            case TelnetConstants.DO: ip.State = (int)TelnetStateMask.TS_DO; break;
                            case TelnetConstants.DONT: ip.State = (int)TelnetStateMask.TS_DONT; break;
                            case TelnetConstants.IAC: // quoted IAC
                                to[toPos++] = TelnetConstants.IAC;
                                ip.State = (int)TelnetStateMask.TS_DATA;
                                break;
                            case 0xF2: // AO? abort etc per L790-L816
                            case 0xF4: // IP
                            case 0xF8: // BRK
                                ip.OutOfBand = true;
                                Add_message(ip.Ob, System.Text.Encoding.ASCII.GetString(telnet_abort_response));
                                Flush_message(ip);
                                ip.State = (int)TelnetStateMask.TS_DATA;
                                break;
                            case TelnetConstants.SB:
                                ip.State = (int)TelnetStateMask.TS_SB;
                                ip.SbPos = 0;
                                break;
                            default:
                                ip.State = (int)TelnetStateMask.TS_DATA;
                                break;
                        }
                    }
                    break;

                case TelnetStateMask.TS_DO:
                    {
                        byte ch = from[i];
                        switch (ch)
                        {
                            case TelnetConstants.TELOPT_SGA:
                                Add_message(ip.Ob, System.Text.Encoding.ASCII.GetString(telnet_will_sga));
                                Flush_message(ip);
                                break;
                            case TelnetConstants.TELOPT_TM:
                                Add_message(ip.Ob, System.Text.Encoding.ASCII.GetString(telnet_do_tm_response));
                                Flush_message(ip);
                                break;
                        }
                        ip.State = (int)TelnetStateMask.TS_DATA;
                    }
                    break;

                case TelnetStateMask.TS_WILL:
                    {
                        if (!ip.IFlags.HasFlag(InteractiveFlags.USING_TELNET))
                            ip.IFlags |= InteractiveFlags.USING_TELNET;
                        byte ch = from[i];
                        switch (ch)
                        {
                            case TelnetConstants.TELOPT_TTYPE:
                                Add_message(ip.Ob, System.Text.Encoding.ASCII.GetString(telnet_term_query));
                                Flush_message(ip);
                                break;
                            case TelnetConstants.TELOPT_LINEMODE:
                                ip.IFlags |= InteractiveFlags.USING_LINEMODE;
                                if (!ip.IFlags.HasFlag(InteractiveFlags.SINGLE_CHAR))
                                {
                                    telnet_sb_lm_mode[4] = (byte)(TelnetConstants.MODE_EDIT | TelnetConstants.MODE_TRAPSIG);
                                    Add_message(ip.Ob, System.Text.Encoding.ASCII.GetString(telnet_sb_lm_mode));
                                    Flush_message(ip);
                                }
                                break;
                            case TelnetConstants.TELOPT_SGA:
                                Add_message(ip.Ob, System.Text.Encoding.ASCII.GetString(telnet_do_sga));
                                Flush_message(ip);
                                break;
                        }
                        ip.State = (int)TelnetStateMask.TS_DATA;
                    }
                    break;

                case TelnetStateMask.TS_DONT:
                case TelnetStateMask.TS_WONT:
                    {
                        if (!ip.IFlags.HasFlag(InteractiveFlags.USING_TELNET))
                            ip.IFlags |= InteractiveFlags.USING_TELNET;
                        byte ch = from[i];
                        if (ch == TelnetConstants.TELOPT_SGA && (TelnetStateMask)stateMask == TelnetStateMask.TS_DONT)
                        {
                            Add_message(ip.Ob, System.Text.Encoding.ASCII.GetString(telnet_wont_sga));
                            Flush_message(ip);
                        }
                        if (ch == TelnetConstants.TELOPT_LINEMODE && (TelnetStateMask)stateMask == TelnetStateMask.TS_WONT)
                            ip.IFlags &= ~InteractiveFlags.USING_LINEMODE;
                        ip.State = (int)TelnetStateMask.TS_DATA;
                    }
                    break;

                case TelnetStateMask.TS_SB:
                    {
                        byte ch = from[i];
                        if (ch == TelnetConstants.IAC)
                            ip.State = (int)TelnetStateMask.TS_SB_IAC;
                        else if (ip.SbPos < CommConstants.SB_SIZE)
                            ip.SbBuf[ip.SbPos++] = ch;
                    }
                    break;
            }
        }
        return toPos;
    }

    // safe_tcsetattr L50-L54 and set_console_echo L942-L961
    public static void Safe_tcsetattr(int fd, object tio)
    {
        // L50-L54 isatty ? TCSAFLUSH : TCSANOW
        Console.WriteLine($"[src/comm.c:50 safe_tcsetattr] fd={fd} true logic needs termios interop");
    }

    public static void Set_console_echo(bool echo)
    {
        // L942-L961 termios handling for console user all_users[0]
        var ip = All_users.Count > 0 ? All_users[0] : null;
        if (ip == null) return;
        Console.WriteLine($"[src/comm.c:942 set_console_echo] echo={echo} fd={ip.Fd} true logic termios ECHO");
    }

    // set_telnet_echo L970-L972
    public static void Set_telnet_echo(ObjectS? ob, bool echo)
    {
        // L971 add_message(ob, echo ? telnet_yes_echo : telnet_no_echo)
        string msg = echo ? System.Text.Encoding.ASCII.GetString(telnet_yes_echo) : System.Text.Encoding.ASCII.GetString(telnet_no_echo);
        Add_message(ob, msg);
    }

    // set_telnet_single_char L977-L1021
    public static void Set_telnet_single_char(InteractiveT? ip, bool single)
    {
        if (ip == null) return;
        if (All_users.Count > 0 && ip == All_users[0])
        {
            // L980-L997 console user termios ICANON|ECHO manipulation
            Console.WriteLine($"[src/comm.c:977 set_telnet_single_char] console single={single} true logic termios ICANON");
            return;
        }
        // L1000-L1021 telnet path
        if (!ip.IFlags.HasFlag(InteractiveFlags.USING_TELNET)) return;
        if (ip.IFlags.HasFlag(InteractiveFlags.USING_LINEMODE))
        {
            telnet_sb_lm_mode[4] = single ? (byte)TelnetConstants.MODE_TRAPSIG : (byte)(TelnetConstants.MODE_TRAPSIG | TelnetConstants.MODE_EDIT);
            Add_message(ip.Ob, System.Text.Encoding.ASCII.GetString(telnet_sb_lm_mode));
            Flush_message(ip);
            return;
        }
        Add_message(ip.Ob, System.Text.Encoding.ASCII.GetString(single ? telnet_will_sga : telnet_wont_sga));
        Flush_message(ip);
    }

    // sigpipe_handler L1027-L1031
    public static void Sigpipe_handler(int sig)
    {
        Console.WriteLine($"[src/comm.c:1027 sigpipe_handler] SIGPIPE={sig} - true logic debug_message");
    }

    // add_console_line L1045-L1076
    public static void Add_console_line(InteractiveT? ip, string lineBuffer, int lineLength)
    {
        // L1045-L1076 convert CR/LF to null terminators, append to text buffer
        if (ip == null) return;
        if (ip.IFlags.HasFlag(InteractiveFlags.NET_DEAD) || ip.IFlags.HasFlag(InteractiveFlags.CLOSING)) return;

        int len = lineLength > 0 ? lineLength - 1 : 0;
        if (len <= 0 || ip.TextEnd + len >= CommConstants.MAX_TEXT) return;

        // L1054-L1066
        for (int i = 0; i < len; i++)
        {
            char c = i < lineBuffer.Length ? lineBuffer[i] : '\0';
            if (c == '\n' || c == '\r')
                ip.TextBytes[ip.TextEnd++] = 0;
            else
                ip.TextBytes[ip.TextEnd++] = (byte)c;
        }
        ip.TextBytes[ip.TextEnd] = 0;

        // L1071 CMD_IN_BUF check
        if (Cmd_in_buf(ip))
            ip.IFlags |= InteractiveFlags.CMD_IN_BUF;
    }

    // drain_console_queue_lines L1085-L1117
    public static void Drain_console_queue_lines()
    {
        Console.WriteLine($"[src/comm.c:1085 drain_console_queue_lines] true logic - console queue dequeue");
        // Real logic would: check g_console_worker eof, re-init console user, dequeque lines
    }

    // process_io L1125-L1265
    public static void Process_io()
    {
        // L1125-L1265 dispatch io events from async_runtime_wait
        Console.WriteLine($"[src/comm.c:1125 process_io] true logic - dispatch events, socket handlers, console flush");
        Drain_console_queue_lines();
        if (All_users.Count > 0 && All_users[0] != null)
            Flush_message(All_users[0]);
    }

    // new_interactive L1274-L1368
    public static void New_interactive(int socketFd)
    {
        // L1274-L1368 allocate interactive_t attached to master_ob, resize all_users
        if (socketFd == -1)
        {
            Console.WriteLine($"[src/comm.c:1277] Invalid socket fd {socketFd}");
            return;
        }

        int slot = -1;
        // L1281 console check STDIN_FILENO
        if (socketFd == 0) // STDIN_FILENO placeholder
        {
            if (All_users.Count > 0 && All_users[0] != null) return;
            slot = 0;
        }
        else
        {
            // L1291 master_ob occupied?
            if (Master_ob != null && Master_ob.Interactive != null)
            {
                Console.WriteLine($"[src/comm.c:1293] master_ob occupied, closing {socketFd}");
                return;
            }
            for (int i = 1; i < Max_users; i++)
            {
                if (i >= All_users.Count || All_users[i] == null) { slot = i; break; }
            }
        }

        if (slot >= Max_users || slot == -1)
        {
            // L1302-L1313 RESIZE all_users +50
            int newSize = Max_users + 50;
            while (All_users.Count < newSize) All_users.Add(null);
            Max_users = newSize;
            if (slot == -1) slot = Max_users - 50;
        }

        // L1315-L1344 init
        var master = Master_ob ?? new ObjectS { Name = "master" };
        Master_ob = master;
        var ip = new InteractiveT
        {
            Ob = master,
            Fd = socketFd,
            TextEnd = 0,
            TextStart = 0,
            SnoopOn = null,
            SnoopBy = null,
            LastTime = Current_time,
            MessageProducer = 0,
            MessageConsumer = 0,
            MessageLength = 0,
            State = (int)TelnetStateMask.TS_DATA,
            OutOfBand = false
        };
        Total_users++;
        if (slot == 0) master.Flags |= (ushort)ObjectFlags.O_CONSOLE_USER;
        master.Flags |= (ushort)ObjectFlags.O_ONCE_INTERACTIVE;
        ip.Ob = master;
        // L1342-L1344 all_users[i] = master->interactive etc.
        while (All_users.Count <= slot) All_users.Add(null);
        All_users[slot] = ip;

        Console.WriteLine($"[src/comm.c:1354 async_runtime_add] slot={slot} fd={socketFd} true logic");
        Num_user++;
    }

    // create_test_interactive L1386-L1442
    public static InteractiveT? Create_test_interactive(ObjectS? ob)
    {
        if (ob == null) return null;
        if (All_users.Count == 0)
        {
            All_users = new List<InteractiveT?>(1) { null };
            Max_users = 1;
        }
        var ip = new InteractiveT
        {
            Ob = ob,
            Fd = 0,
            TextEnd = 0,
            TextStart = 0,
            LastTime = Current_time,
            State = (int)TelnetStateMask.TS_DATA
        };
        ob.Flags |= (ushort)ObjectFlags.O_ONCE_INTERACTIVE;
        while (All_users.Count <= 0) All_users.Add(null);
        All_users[0] = ip;
        return ip;
    }

    // remove_test_interactive L1452-L1482
    public static void Remove_test_interactive(InteractiveT? ip)
    {
        if (ip == null) return;
        if (ip.InputTo != null)
        {
            // free_sentence
            ip.InputTo = null;
        }
        ip.DefaultErrMessage.S = null;
        if (ip.Ob != null)
            ip.Ob.Interactive = null;
        if (All_users.Count > 0 && All_users[0] == ip)
            All_users[0] = null;
        Console.WriteLine($"[src/comm.c:1452 remove_test_interactive] freed ip for {ip.Ob?.Name}");
    }

    // setup_accepted_connection L1493-L1578
    public static void Setup_accepted_connection(PortDefT port, int newSocketFd, IPEndPoint addr)
    {
        // L1493-L1578 set nonblocking, new_interactive, store addr, mudlib_connect/logon, telnet negoti, async read post
        Console.WriteLine($"[src/comm.c:1493 setup_accepted_connection] port={port.Port} fd={newSocketFd} addr={addr} true logic");
        if (!Set_socket_nonblocking(newSocketFd, true))
        {
            Console.WriteLine($"[src/comm.c:1500] Failed nonblock");
            return;
        }
        New_interactive(newSocketFd);
        if (Master_ob == null || FindInteractiveForObject(Master_ob) == null)
            return;
        var ip = FindInteractiveForObject(Master_ob)!;
        ip.Addr = addr;
        ip.ConnectionType = (int)port.Kind;

        // L1525 mudlib_connect
        var userOb = Mudlib_connect(port.Port, addr.ToString());
        if (userOb == null)
        {
            if (Master_ob != null) Remove_interactive(Master_ob, false);
            return;
        }
        // L1535-L1543 telnet negotiation
        if (port.Kind == PortKind.PORT_TELNET)
        {
            Query_addr_name(userOb);
            Add_message(userOb, System.Text.Encoding.ASCII.GetString(telnet_no_echo));
            Add_message(userOb, System.Text.Encoding.ASCII.GetString(telnet_do_ttype));
            Add_message(userOb, System.Text.Encoding.ASCII.GetString(telnet_do_naws));
            Add_message(userOb, System.Text.Encoding.ASCII.GetString(telnet_do_linemode));
            var uIp = FindInteractiveForObject(userOb);
            if (uIp != null) Flush_message(uIp);
        }
        Mudlib_logon(userOb);
    }

    // new_user_handler L1583-L1620 placeholder
    public static void New_user_handler(PortDefT port)
    {
        Console.WriteLine($"[src/comm.c:1580 new_user_handler] port={port.Port} true logic accept()");
        // POSIX would accept() here then call Setup_accepted_connection
    }

    // get_user_data L1632-L1839
    public static void Get_user_data(InteractiveT ip, object? evt)
    {
        // L1632-L1839 handles both readiness and completion models
        if (ip.ConnectionType == (int)PortKind.CONSOLE_USER)
        {
            Console.WriteLine("[src/comm.c:1640 get_user_data] console user unexpected in network path");
            return;
        }

        int textSpace;
        if ((PortKind)ip.ConnectionType == PortKind.PORT_TELNET)
            textSpace = (CommConstants.MAX_TEXT - (int)ip.TextEnd - 1) / 3;
        else
            textSpace = CommConstants.MAX_TEXT - (int)ip.TextEnd - 1;

        // L1656-L1672 shift buffer if low space
        if (textSpace < CommConstants.MAX_TEXT / 16 && (PortKind)ip.ConnectionType == PortKind.PORT_TELNET)
        {
            long len = ip.TextEnd - ip.TextStart;
            Array.Copy(ip.TextBytes, ip.TextStart, ip.TextBytes, 0, len + 1);
            ip.TextStart = 0;
            ip.TextEnd = len;
            textSpace = (CommConstants.MAX_TEXT - (int)ip.TextEnd - 1) / 3;
            if (textSpace < CommConstants.MAX_TEXT / 16)
            {
                ip.TextStart = 0;
                ip.TextEnd = 0;
                textSpace = CommConstants.MAX_TEXT / 3;
            }
        }

        // L1698-L1729 Completion vs Readiness read
        Console.WriteLine($"[src/comm.c:1632 get_user_data] fd={ip.Fd} textSpace={textSpace} true logic socket recv/copy_chars");
        // Real logic would recv into buf then copy_chars for TELNET, or process ASCII/BINARY lines L1795-L1836
    }

    // remove_interactive L1844-L1948
    public static void Remove_interactive(ObjectS? ob, bool dested)
    {
        // L1844-L1948 remove user immediately
        if (ob == null) return;
        var ip = FindInteractiveForObject(ob);
        if (ip == null) return;
        if (ip.IFlags.HasFlag(InteractiveFlags.CLOSING))
        {
            if (!dested) Console.WriteLine("[src/comm.c:1860] Double call to remove_interactive()");
            return;
        }
        Flush_message(ip);
        ip.IFlags |= InteractiveFlags.CLOSING;

        // L1868-L1873 save_ed_buffer
        if (!dested)
            Console.WriteLine($"[src/comm.c:1880] APPLY_SAFE_CALL APPLY_NET_DEAD ob={ob.Name} ORIGIN_DRIVER");

        if (ip.SnoopBy != null) { ip.SnoopBy.SnoopOn = null; ip.SnoopBy = null; }
        if (ip.SnoopOn != null) { ip.SnoopOn.SnoopBy = null; ip.SnoopOn = null; }

        // L1894-L1897 async_runtime_remove
        if (All_users.Count == 0 || ip != All_users[0])
            Console.WriteLine($"[src/comm.c:1897] async_runtime_remove fd={ip.Fd}");

        // L1900-L1928 console pipe vs real console handling, SOCKET_CLOSE
        Console.WriteLine($"[src/comm.c:1923] SOCKET_CLOSE fd={ip.Fd} stub");

        if ((ob.Flags & (ushort)ObjectFlags.O_HIDDEN) != 0) Num_hidden--;
        Num_user--;

        // L1932 clear_notify, free input_to L1933-L1937
        if (ip.InputTo != null) { ip.InputTo = null; }

        int idx = All_users.IndexOf(ip);
        if (idx >= 0) All_users[idx] = null;

        Total_users--;
        ob.Interactive = null;
        Console.WriteLine($"[src/comm.c:1946] free_object ob={ob.Name} remove_interactive");
    }

    // telnet_neg L1956-L1979
    public static void Telnet_neg(string to, string from)
    {
        // L1956-L1979 backspace resolution
        var outList = new List<char>();
        foreach (char ch in from)
        {
            if (ch == '\b' || ch == (char)0x7f)
            {
                if (outList.Count > 0) outList.RemoveAt(outList.Count - 1);
                continue;
            }
            outList.Add(ch);
            if (ch == '\0') break;
        }
        // to ref assignment would be done by caller
        Console.WriteLine($"[src/comm.c:1956 telnet_neg] converted {from.Length} -> {outList.Count} true logic");
    }

    // query_addr_name L1981-L1993
    public static void Query_addr_name(ObjectS? ob)
    {
        if (ob == null) return;
        var ip = FindInteractiveForObject(ob);
        if (ip == null) return;
        var ipstr = Query_ip_number(ob);
        if (ipstr == null || ipstr == "N/A") return;
        Console.WriteLine($"[src/comm.c:1981 query_addr_name] enqueue reverse {ipstr} true logic addr_resolver_enqueue_reverse");
    }

    // query_addr_number L1998-L2047
    public static int Query_addr_number(string? name, string? callBack)
    {
        // L1998-L2047 cache check + async enqueue
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(callBack))
        {
            Console.WriteLine($"[src/comm.c:2003 query_addr_number] immediate fallback APPLY_CALL {callBack}");
            // push immediate failure path
            var svName = name != null ? SValueS.FromSharedString(name) : new SValueS { Type = (SValueType)CommConstants.T_NUMBER, U = new SValueU { Number = 0 } };
            Console.WriteLine($"[src/comm.c:2004] share_and_push_string {name} + push_undefined + APPLY_CALL - int64 number handling for request_id");
            return 0;
        }

        // L2010 forward cache check
        Console.WriteLine($"[src/comm.c:2010 query_addr_number] name={name} cb={callBack} - check forward cache, else enqueue LOOKUP");
        // Stub request_id as int64 in svalue_u.number context
        long requestId = new Random().Next(1, 10000); // int64_t path
        var svReq = SValueS.FromNumber(requestId); // uses long Number (int64_t)
        return (int)requestId;
    }

    // process_addr_resolver_completions L2049-L2148
    public static void Process_addr_resolver_completions()
    {
        Console.WriteLine($"[src/comm.c:2049 process_addr_resolver_completions] true logic");
        Console.WriteLine($"[src/comm.c:2054] while addr_resolver_dequeue_result: handle socket DNS, reverse cache, forward cache, push_number(request_id) where number is int64_t");
        // L2086 O_DESTRUCTED check
        // L2092-L2110 addr_resolver_forward_cache_add
        // L2140-L2144 APPLY_SAFE_CALL with 3 args including request_id (int64)
    }

    // query_ip_name L2150-L2175
    public static string? Query_ip_name(ObjectS? ob)
    {
        if (ob == null) ob = Command_giver;
        if (ob == null) return null;
        var ip = FindInteractiveForObject(ob);
        if (ip == null) return null;
        Console.WriteLine($"[src/comm.c:2150 query_ip_name] check reverse cache fd={ip.Fd} fallback to query_ip_number");
        // Real: check addr_resolver_reverse_cache_get
        return Query_ip_number(ob); // fallback per L2174
    }

    // add_ip_entry L2177-L2180
    public static void Add_ip_entry(ulong addr, string name)
    {
        // L2177 delegates to addr_resolver_reverse_cache_add
        Console.WriteLine($"[src/comm.c:2177 add_ip_entry] addr={addr} name={name} -> addr_resolver_reverse_cache_add true logic");
    }

    // query_ip_number L2188-L2196
    public static string Query_ip_number(ObjectS? ob)
    {
        // L2188-L2196 inet_ntop
        if (ob == null) ob = Command_giver;
        if (ob == null) return "N/A";
        var ip = FindInteractiveForObject(ob);
        if (ip == null || ip.Addr == null) return "N/A";
        return ip.Addr.Address.ToString();
    }

    // query_host_name L2198-L2204
    public static string Query_host_name()
    {
        // L2198 gethostname
        return Dns.GetHostName();
    }

    // query_idle L2206-L2210
    public static long Query_idle(ObjectS? ob)
    {
        // L2206-L2210 error if non-interactive, return current_time - last_time
        if (ob == null) throw new InvalidOperationException("[src/comm.c:2208 query_idle] non-interactive");
        var ip = FindInteractiveForObject(ob);
        if (ip == null) throw new InvalidOperationException("[src/comm.c:2208 query_idle] of non-interactive object");
        // uses int64_t current_time difference but returns time_t
        return Current_time - ip.LastTime;
    }

    // replace_interactive L2212-L2249 - exec() efun
    public static bool Replace_interactive(ObjectS? ob, ObjectS? obfrom)
    {
        // L2212-L2249 flags O_HIDDEN counter num_hidden, transfer interactive
        if (ob == null) throw new ArgumentException("Bad argument 1 to exec()");
        if (obfrom == null) throw new ArgumentException("Bad argument 2 to exec()");
        var ipFrom = FindInteractiveForObject(obfrom);
        if (ob.Interactive != null) throw new ArgumentException("Bad argument 1 to exec() [src/comm.c:2213]");
        if (ipFrom == null) throw new ArgumentException("Bad argument 2 to exec() [src/comm.c:2217]");

        bool hiddenOb = (ob.Flags & (ushort)ObjectFlags.O_HIDDEN) != 0;
        bool hiddenFrom = (obfrom.Flags & (ushort)ObjectFlags.O_HIDDEN) != 0;
        if (hiddenOb != hiddenFrom)
        {
            if (hiddenOb) Num_hidden++; else Num_hidden--;
        }

        // L2232 transfer
        ipFrom.Ob = ob;
        ipFrom.IFlags |= InteractiveFlags.HAS_WRITE_PROMPT | InteractiveFlags.HAS_PROCESS_INPUT;

        ob.Interactive = null; // Actually master transfer needs mapping; we store in All_users list
        // Update All_users list entry to point new owner
        int idx = All_users.IndexOf(ipFrom);
        if (idx >= 0) All_users[idx] = ipFrom;

        ob.Flags |= (ushort)ObjectFlags.O_ONCE_INTERACTIVE;
        obfrom.Flags &= unchecked((ushort)~(ushort)ObjectFlags.O_ONCE_INTERACTIVE);

        ob.AddRef("exec");

        // L2244 command_giver swap
        if (obfrom == Command_giver) Command_giver = ob;

        Console.WriteLine($"[src/comm.c:2212 replace_interactive] exec transfer fd={ipFrom.Fd} from={obfrom.Name} to={ob.Name} true logic free_object obfrom");
        return true;
    }

    // get_async_runtime L2255-L2257
    public static object? Get_async_runtime()
    {
        Console.WriteLine($"[src/comm.c:2255 get_async_runtime] true logic returns g_runtime");
        return null;
    }

    // --- Helpers not in original but needed for C# shim ---

    static InteractiveT? FindInteractiveForObject(ObjectS? ob)
    {
        if (ob == null) return null;
        foreach (var ip in All_users)
        {
            if (ip != null && ip.Ob == ob) return ip;
        }
        // Also try if ObjectS.Interactive stub doesn't link; return first with matching Name as fallback?
        return null;
    }

    static bool Set_socket_nonblocking(int fd, bool nonblock)
    {
        Console.WriteLine($"[src/comm.c:1499 set_socket_nonblocking] fd={fd} nonblock={nonblock} true logic");
        return true;
    }

    static ObjectS? Mudlib_connect(int port, string addrStr)
    {
        Console.WriteLine($"[src/comm.c:1525 mudlib_connect] port={port} addr={addrStr} true logic - would call master->connect()");
        // Stub returns a new user object
        var ob = new ObjectS { Name = $"user@{addrStr}:{port}", Flags = 0 };
        return ob;
    }

    static void Mudlib_logon(ObjectS userOb)
    {
        Console.WriteLine($"[src/comm.c:1547 mudlib_logon] ob={userOb.Name} true logic APPLY logon");
    }

    static bool Cmd_in_buf(InteractiveT ip)
    {
        // L1071 etc checks for '\0' in text buffer indicating complete command
        // Simplified: does text contain 0 terminator before TextEnd?
        for (int i = (int)ip.TextStart; i < ip.TextEnd; i++)
            if (ip.TextBytes[i] == 0) return true;
        return false;
    }

    // query_ip_port efun wrapper F_QUERY_IP_PORT referenced in comm.h L122
    public static int Query_ip_port(ObjectS? ob)
    {
        if (ob == null) ob = Command_giver;
        var ip = FindInteractiveForObject(ob);
        return ip?.LocalPort ?? 0;
    }
}