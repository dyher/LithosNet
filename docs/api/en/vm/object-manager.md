# ObjectManager - English

## Function
Port of backend.c / object.c object table

## API
- ResolvePath(lpcPath): /obj/master -> MudlibRoot/obj/master.c
- LoadObject(lpcPath): compile+load, auto call create(), detect Master/SimulEfun
- FindObject: lookup
- Preload: load list from master->preload()

## Flow
LoadObject -> Preprocessor -> Lexer -> Parser.ParseFunctions() -> CodeGenerator.GenerateProgram() -> LpcObject -> InterpreterV4.CallApply("create", [], obj)

## Fixed
CallApply(obj,"create") wrong order, fixed to CallApply("create", Empty, obj)
