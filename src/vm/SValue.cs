namespace LithosNet.VM;
// 1:1 from taedlar/neolith lib/lpc/svalue.h + types.h
// Runtime: int64_t number per docs/internals/int64-design.md
public enum SType { T_NUMBER=0, T_STRING=1, T_REAL=2, T_ARRAY=3, T_MAPPING=4, T_OBJECT=5, T_FUNCTION=6, T_BUFFER=7, T_INVALID=255 }
public sealed class SValue{
  public SType Type;
  public long Number; // int64_t
  public double Real;
  public string? Str;
  public object? Ref;
  public static SValue Nil => new(){ Type=SType.T_INVALID };
  public static SValue FromNumber(long n)=>new(){ Type=SType.T_NUMBER, Number=n };
  public static SValue FromString(string s)=>new(){ Type=SType.T_STRING, Str=s };
}
// C++ 慣用: lpc::svalue owning + svalue_view borrowing
public sealed class SValueView{
  readonly SValue _s;
  SValueView(SValue s){_s=s;}
  public static SValueView From(SValue s)=>new(s);
  public bool IsString()=>_s.Type==SType.T_STRING;
  public bool IsNumber()=>_s.Type==SType.T_NUMBER;
  public string CStr()=>_s.Str??"";
  public long Number()=>_s.Number;
}
