# ObjectManager - 中文

## 功能
仿 backend.c / object.c 物件表

## API
- ResolvePath(lpcPath): /obj/master -> MudlibRoot/obj/master.c
- LoadObject(lpcPath): 編譯+載入，自動呼叫 create()，偵測 Master/SimulEfun
- FindObject: 查表
- Preload: 載 master->preload() 列表

## 流程
LoadObject -> Preprocessor -> Lexer -> Parser.ParseFunctions() -> CodeGenerator.GenerateProgram() -> LpcObject -> InterpreterV4.CallApply("create", [], obj)

## 已修
CallApply(obj,"create") 參數順序錯，已修為 CallApply("create", Empty, obj)
