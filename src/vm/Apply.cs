
namespace LithosNet.V4.VM;
// 1:1 from taedlar/neolith src/apply.h

public static class ApplyCache {
    public const int APPLY_CACHE_BITS = 10; // typical
    public const int APPLY_CACHE_SIZE = 1 << APPLY_CACHE_BITS;
    public static bool MasterApproved(SValueS? sv){
        // MASTER_APPROVED(x) (((x)==(svalue_t *)-1) || ((x) && (((x)->type != T_NUMBER) || (x)->u.number)))
        if(sv == null) return false; // -1 sentinel not modeled yet
        if(sv.Type != SValueType.T_NUMBER) return true;
        return sv.U.Number != 0;
    }
}

public sealed class ApplyManager {
    // svalue_t *apply_call(const char *, object_t *, int, int, bool) from apply.h L54
    public SValueS? ApplyCall(string fun, ObjectS ob, int numArg, int where, bool slot){
        // APPLY_CALL vs APPLY_SLOT_CALL per L24-L46
        // After applies, always re-check ob->flags & O_DESTRUCTED per copilot-instructions L27
        if(ob.IsDestructed) return null;
        // TODO lookup function via find_function from apply.h L61
        return null;
    }
}
