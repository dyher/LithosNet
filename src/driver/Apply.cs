namespace LithosNet.Driver;

/// <summary>
/// 1:1 port of neolith src/applies.h
/// </summary>
public static class Apply
{
    public const string GlobalInit = "#global_init#"; // APPLY___INIT

    // all objects
    public const string AuthorFile = "author_file";
    public const string CleanUp = "clean_up";
    public const string Create = "create";
    public const string Id = "id";
    public const string Init = "init";
    public const string MoveOrDestruct = "move_or_destruct";
    public const string Reset = "reset";

    // master object
    public const string CompileObject = "compile_object";
    public const string Connect = "connect";
    public const string Crash = "crash";
    public const string CreatorFile = "creator_file";
    public const string DomainFile = "domain_file";
    public const string Epilog = "epilog";
    public const string ErrorHandler = "error_handler";
    public const string GetBbUid = "get_bb_uid";
    public const string GetEdBufferSaveFileName = "get_save_file_name";
    public const string GetRootUid = "get_root_uid";
    public const string LogError = "log_error";
    public const string MakePathAbsolute = "make_path_absolute";
    public const string Flag = "flag";
    public const string ObjectName = "object_name";
    public const string ParserErrorMessage = "parser_error_message";
    public const string Preload = "preload";
    public const string PrivsFile = "privs_file";
    public const string SlowShutdown = "slow_shutdown";
    public const string ValidAsm = "valid_asm";
    public const string ValidBind = "valid_bind";
    public const string ValidCompileToC = "valid_compile_to_c";
    public const string ValidHide = "valid_hide";
    public const string ValidLink = "valid_link";
    public const string ValidObject = "valid_object";
    public const string ValidOverride = "valid_override";
    public const string ValidRead = "valid_read";
    public const string ValidSaveBinary = "valid_save_binary";
    public const string ValidSeteuid = "valid_seteuid";
    public const string ValidShadow = "valid_shadow";
    public const string ValidSocket = "valid_socket";
    public const string ValidWrite = "valid_write";

    // interactive
    public const string CatchTell = "catch_tell";
    public const string Logon = "logon";
    public const string NetDead = "net_dead";
    public const string InputPrompt = "input_prompt";
    public const string ProcessInput = "process_input";
    public const string ReceiveMessage = "receive_message";
    public const string ReceiveSnoop = "receive_snoop";
    public const string TelnetSuboption = "telnet_suboption";
    public const string TerminalType = "set_terminal_type";
    public const string WindowSize = "set_window_size";
    public const string WritePrompt = "write_prompt";
    public const string RetrieveEdSetup = "retrieve_ed_setup";
    public const string SaveEdSetup = "save_ed_setup";

    public static readonly string[] AllMasterApplies = new[]
    {
        CompileObject, Connect, Crash, CreatorFile, DomainFile, Epilog, ErrorHandler,
        GetBbUid, GetEdBufferSaveFileName, GetRootUid, LogError, MakePathAbsolute, Flag,
        ObjectName, ParserErrorMessage, Preload, PrivsFile, SlowShutdown,
        ValidAsm, ValidBind, ValidCompileToC, ValidHide, ValidLink, ValidObject,
        ValidOverride, ValidRead, ValidSaveBinary, ValidSeteuid, ValidShadow, ValidSocket, ValidWrite
    };

    public static readonly string[] AllObjectApplies = new[]
    {
        AuthorFile, CleanUp, Create, Id, Init, MoveOrDestruct, Reset
    };

    public static readonly string[] AllInteractiveApplies = new[]
    {
        CatchTell, Logon, NetDead, InputPrompt, ProcessInput, ReceiveMessage, ReceiveSnoop,
        TelnetSuboption, TerminalType, WindowSize, WritePrompt, RetrieveEdSetup, SaveEdSetup
    };
}
