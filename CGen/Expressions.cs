using System;
using System.Collections.Generic;
using System.Linq;
using CodeGeneration;

namespace ABT {
    public abstract partial class Expr {
        public abstract Reg CGenValue(CGenState state);

        public abstract void CGenAddress(CGenState state);
    }

    public sealed partial class Variable {
        public override void CGenAddress(CGenState state) {
            Env.Entry entry = this.Env.Find(this.Name).Value;
            Int32 offset = entry.Offset;

            switch (entry.Kind) {
                case Env.EntryKind.FRAME:
                case Env.EntryKind.STACK:
                    state.LEA(offset, Reg.EBP, Reg.EAX);
                    return;

                case Env.EntryKind.GLOBAL:
                    state.LEA(this.Name, Reg.EAX);
                    return;

                case Env.EntryKind.ENUM:
                case Env.EntryKind.TYPEDEF:
                default:
                    throw new InvalidProgramException("cannot get the address of " + entry.Kind);
            }
        }

        public override Reg CGenValue(CGenState state) {
            Env.Entry entry = this.Env.Find(this.Name).Value;

            Int32 offset = entry.Offset;

            switch (entry.Kind) {
                case Env.EntryKind.ENUM:
                    // 1. If the variable is an enum constant,
                    //    return the Value in %eax.
                    state.MOV(offset, Reg.EAX);
                    return Reg.EAX;

                case Env.EntryKind.FRAME:
                case Env.EntryKind.STACK:
                    // 2. If the variable is a function argument or a local variable,
                    //    the address would be offset(%ebp).
                    switch (this.Type.Kind) {
                        case ExprTypeKind.LONG:
                        case ExprTypeKind.ULONG:
                        case ExprTypeKind.POINTER:
                            // %eax = offset(%ebp)
                            state.MOV(offset, Reg.EBP, Reg.EAX);
                            return Reg.EAX;

                        case ExprTypeKind.FLOAT:
                        case ExprTypeKind.DOUBLE:
                        case ExprTypeKind.STRUCT_OR_UNION:
                            throw new InvalidProgramException("floats and structs are not supported");
                        case ExprTypeKind.VOID:
                            throw new InvalidProgramException("How could a variable be void?");
                        // %eax = $0
                        // state.MOV(0, Reg.EAX);
                        // return Reg.EAX;

                        case ExprTypeKind.FUNCTION:
                            throw new InvalidProgramException("How could a variable be a function designator?");

                        case ExprTypeKind.CHAR:
                        case ExprTypeKind.UCHAR:
                        case ExprTypeKind.SHORT:
                        case ExprTypeKind.USHORT:
                            state.MOV(offset, Reg.EBP, Reg.EAX);
                            return Reg.EAX;

                        case ExprTypeKind.ARRAY:
                            // %eax = (off(%ebp))
                            state.LEA(offset, Reg.EBP, Reg.EAX); // source address
                            return Reg.EAX;

                        default:
                            throw new InvalidOperationException($"Cannot get value of {this.Type.Kind}");
                    }

                case Env.EntryKind.GLOBAL:
                    switch (this.Type.Kind) {
                        case ExprTypeKind.CHAR:
                            state.MOV(this.Name, Reg.EAX);
                            return Reg.EAX;

                        case ExprTypeKind.UCHAR:
                            state.MOV(this.Name, Reg.EAX);
                            return Reg.EAX;

                        case ExprTypeKind.SHORT:
                            state.MOV(this.Name, Reg.EAX);
                            return Reg.EAX;

                        case ExprTypeKind.USHORT:
                            state.MOV(this.Name, Reg.EAX);
                            return Reg.EAX;

                        case ExprTypeKind.LONG:
                        case ExprTypeKind.ULONG:
                        case ExprTypeKind.POINTER:
                            state.MOV(this.Name, Reg.EAX);
                            return Reg.EAX;

                        case ExprTypeKind.FUNCTION:
                            state.MOV("$" + this.Name, Reg.EAX);
                            return Reg.EAX;

                        case ExprTypeKind.FLOAT:
                        case ExprTypeKind.DOUBLE:
                        case ExprTypeKind.STRUCT_OR_UNION: 
                            throw new InvalidProgramException("floats and structs are not supported");

                        //state.LEA(name, Reg.ESI); // source address
                        //state.CGenExpandStackBy(Utils.RoundUp(Type.SizeOf, 4));
                        //state.LEA(0, Reg.ESP, Reg.EDI); // destination address
                        //state.MOV(Type.SizeOf, Reg.ECX); // nbytes
                        //state.CGenMemCpy();
                        //return Reg.STACK;

                        case ExprTypeKind.VOID:
                            throw new InvalidProgramException("How could a variable be void?");
                        //state.MOV(0, Reg.EAX);
                        //return Reg.EAX;

                        case ExprTypeKind.ARRAY:
                            state.MOV($"${this.Name}", Reg.EAX);
                            return Reg.EAX;

                        default:
                            throw new InvalidProgramException("cannot get the Value of a " + this.Type.Kind.ToString());
                    }

                case Env.EntryKind.TYPEDEF:
                default:
                    throw new InvalidProgramException("cannot get the Value of a " + entry.Kind);
            }
        }
    }

    public sealed partial class AssignList {
        public override Reg CGenValue(CGenState state) {
            Reg reg = Reg.EAX;
            foreach (Expr expr in this.Exprs) {
                reg = expr.CGenValue(state);
            }
            return reg;
        }

        public override void CGenAddress(CGenState state) {
            throw new InvalidOperationException("Cannot get the address of an assignment list.");
        }
    }

    public sealed partial class Assign {
        public override Reg CGenValue(CGenState state) {
            // 1. %eax = &left
            this.Left.CGenAddress(state);

            // 2. push %eax
            Int32 pos = state.CGenPushLong(Reg.EAX);

            Reg ret = this.Right.CGenValue(state);
            switch (this.Left.Type.Kind) {
                case ExprTypeKind.CHAR:
                case ExprTypeKind.UCHAR:
                case ExprTypeKind.SHORT:
                case ExprTypeKind.USHORT:
                case ExprTypeKind.LONG:
                case ExprTypeKind.ULONG:
                case ExprTypeKind.POINTER:
                    // pop bx
                    // now bx = &Left
                    state.CGenPopLong(pos, Reg.EBX);

                    // *bx = ax
                    state.MOV(Reg.EAX, 0, Reg.EBX);

                    return Reg.EAX;

                case ExprTypeKind.FLOAT:
                case ExprTypeKind.DOUBLE:
                case ExprTypeKind.STRUCT_OR_UNION:
                    throw new InvalidProgramException("structures and floats are not supported");

                case ExprTypeKind.FUNCTION:
                case ExprTypeKind.VOID:
                case ExprTypeKind.ARRAY:
                case ExprTypeKind.INCOMPLETE_ARRAY:
                default:
                    throw new InvalidProgramException("cannot assign to a " + this.Type.Kind.ToString());
            }
        }
    
        public override void CGenAddress(CGenState state) {
            throw new InvalidOperationException("Cannot get the address of an assignment expression.");
        }
    }

    public sealed partial class ConditionalExpr {
        // 
        //          test Cond
        //          jz false ---+
        //          true_expr   |
        // +------- jmp finish  |
        // |    false: <--------+
        // |        false_expr
        // +--> finish:
        // 
        public override Reg CGenValue(CGenState state) {
            Int32 stack_size = state.StackSize;
            Reg ret = this.Cond.CGenValue(state);
            state.CGenForceStackSizeTo(stack_size);

            // test Cond
            switch (ret) {
                case Reg.EAX:
                    state.CMPL(Reg.EAX, Reg.EAX);
                    break;

                case Reg.ST0:
                    throw new InvalidProgramException("floats and structs are not supported");
                default:
                    throw new InvalidProgramException();
            }

            Int32 false_label = state.RequestLabel();
            Int32 finish_label = state.RequestLabel();

            state.JZ(false_label);

            this.TrueExpr.CGenValue(state);

            state.JMP(finish_label);

            state.CGenLabel(false_label);

            ret = this.FalseExpr.CGenValue(state);

            state.CGenLabel(finish_label);

            return ret;
        }

        public override void CGenAddress(CGenState state) {
            throw new InvalidOperationException("Cannot get the address of a conditional expression.");
        }
    }

    public sealed partial class FuncCall {
        public override void CGenAddress(CGenState state) {
            throw new InvalidOperationException("Error: cannot get the address of a function call.");
        }

        public override Reg CGenValue(CGenState state) {

            // GCC's IA-32 calling convention
            // Caller is responsible to push all arguments to the stack in reverse order.
            // Each argument is aligned to 1 word
            // The return Value is stored in ax
            // 
            // The stack would look like this after pushing all the arguments:
            // +--------+
            // |  ....  |
            // +--------+
            // |  argn  |
            // +--------+
            // |  ....  |
            // +--------+
            // |  arg2  |
            // +--------+
            // |  arg1  |
            // +--------+ <- sp before call
            //

            state.NEWLINE();
            state.COMMENT($"Before pushing the arguments, stack size = {state.StackSize}.");

            var r_pack = Utils.PackArguments(this.Args.Select(_ => _.Type).ToList());
            Int32 pack_size = r_pack.Item1;
            IReadOnlyList<Int32> offsets = r_pack.Item2;

            if (this.Type is StructOrUnionType) {
                // If the function returns a struct

                // Allocate space for return Value.
                state.COMMENT("Allocate space for returning stack.");
                state.CGenExpandStackWithAlignment(this.Type.SizeOf, this.Type.Alignment);

                // Temporarily store the address in ax.
                state.MOV(Reg.ESP, Reg.EAX);

                // add an extra argument and move all other arguments upwards.
                pack_size += ExprType.SIZEOF_POINTER;
                offsets = offsets.Select(_ => _ + ExprType.SIZEOF_POINTER).ToList();
            }

            // Allocate space for arguments.
            // If returning struct, the extra pointer is included.
            state.COMMENT($"Arguments take {pack_size} bytes.");
            state.CGenExpandStackBy(pack_size);
            state.NEWLINE();

            // Store the address as the first argument.
            if (this.Type is StructOrUnionType) {
                throw new InvalidProgramException("floats and structs are not supported");
                state.COMMENT("Putting extra argument for struct return address.");
                state.MOV(Reg.EAX, 0, Reg.ESP);
                state.NEWLINE();
            }

            // This is the stack size before calling the function.
            Int32 header_base = -state.StackSize;

            // Push the arguments onto the stack in reverse order
            for (Int32 i = this.Args.Count; i-- > 0;) {
                Expr arg = this.Args[i];
                Int32 pos = header_base + offsets[i];

                state.COMMENT($"Argument {i} is at {pos}");

                Reg ret = arg.CGenValue(state);
                switch (arg.Type.Kind) {
                    case ExprTypeKind.ARRAY:
                    case ExprTypeKind.CHAR:
                    case ExprTypeKind.UCHAR:
                    case ExprTypeKind.SHORT:
                    case ExprTypeKind.USHORT:
                    case ExprTypeKind.LONG:
                    case ExprTypeKind.ULONG:
                    case ExprTypeKind.POINTER:
                        if (ret != Reg.EAX) {
                            throw new InvalidProgramException();
                        }
                        state.MOV(Reg.EAX, pos, Reg.EBP);
                        break;

                    case ExprTypeKind.DOUBLE:
                    case ExprTypeKind.FLOAT:
                    case ExprTypeKind.STRUCT_OR_UNION:
                        throw new InvalidProgramException("floats and structs are not supported");

                    default:
                        throw new InvalidProgramException();
                }

                state.NEWLINE();

            }

            // When evaluating arguments, the stack might be changed.
            // We must restore the stack.
            state.CGenForceStackSizeTo(-header_base);

            // Get function address
            if (this.Func.Type is FunctionType) {
                this.Func.CGenAddress(state);
            } else if (this.Func.Type is PointerType) {
                this.Func.CGenValue(state);
            } else {
                throw new InvalidProgramException();
            }

            state.CALL("ax");

            state.COMMENT("Function returned.");
            state.NEWLINE();

            if (this.Type.Kind == ExprTypeKind.FLOAT || this.Type.Kind == ExprTypeKind.DOUBLE) {
                throw new InvalidProgramException("floats and structs are not supported");
            }
            return Reg.EAX;
        }
    }

    public sealed partial class Attribute {
        public override Reg CGenValue(CGenState state) {

            // %eax is the address of the struct/union
            if (this.Expr.CGenValue(state) != Reg.EAX) {
                throw new InvalidProgramException();
            }

            if (this.Expr.Type.Kind != ExprTypeKind.STRUCT_OR_UNION) {
                throw new InvalidProgramException();
            }

            // size of the struct or union
            Int32 struct_size = this.Expr.Type.SizeOf;

            // offset inside the pack
            Int32 attrib_offset = ((StructOrUnionType)this.Expr.Type)
                        .Attribs
                        .First(_ => _.name == this.Name)
                        .offset;

            // can't be a function designator.
            switch (this.Type.Kind) {
                case ExprTypeKind.ARRAY:
                    state.ADDL(attrib_offset, Reg.EAX);
                    return Reg.EAX;

                case ExprTypeKind.CHAR:
                    state.MOV(attrib_offset, Reg.EAX, Reg.EAX);
                    return Reg.EAX;

                case ExprTypeKind.UCHAR:
                    state.MOV(attrib_offset, Reg.EAX, Reg.EAX);
                    return Reg.EAX;

                case ExprTypeKind.SHORT:
                    state.MOV(attrib_offset, Reg.EAX, Reg.EAX);
                    return Reg.EAX;

                case ExprTypeKind.USHORT:
                    state.MOV(attrib_offset, Reg.EAX, Reg.EAX);
                    return Reg.EAX;

                case ExprTypeKind.LONG:
                case ExprTypeKind.ULONG:
                case ExprTypeKind.POINTER:
                    state.MOV(attrib_offset, Reg.EAX, Reg.EAX);
                    return Reg.EAX;

                case ExprTypeKind.FLOAT:
                case ExprTypeKind.DOUBLE:
                case ExprTypeKind.STRUCT_OR_UNION:
                    throw new InvalidProgramException("floats and structs are not supported");
                default:
                    throw new InvalidProgramException();
            }
        }

        public override void CGenAddress(CGenState state) {
            if (this.Expr.Type.Kind != ExprTypeKind.STRUCT_OR_UNION) {
                throw new InvalidProgramException();
            }

            // %eax = address of struct or union
            this.Expr.CGenAddress(state);

            // offset inside the pack
            Int32 offset = ((StructOrUnionType)this.Expr.Type)
                        .Attribs
                        .First(_ => _.name == this.Name)
                        .offset;

            state.ADDL(offset, Reg.EAX);
        }
    }

    public sealed partial class Reference {
        public override Reg CGenValue(CGenState state) {
            this.Expr.CGenAddress(state);
            return Reg.EAX;
        }

        public override void CGenAddress(CGenState state) {
            throw new InvalidOperationException("Cannot get the address of a pointer value.");
        }
    }

    public sealed partial class Dereference {
        public override Reg CGenValue(CGenState state) {
            Reg ret = this.Expr.CGenValue(state);
            if (ret != Reg.EAX) {
                throw new InvalidProgramException();
            }
            if (this.Expr.Type.Kind != ExprTypeKind.POINTER) {
                throw new InvalidProgramException();
            }

            ExprType type = ((PointerType)this.Expr.Type).RefType;
            switch (type.Kind) {
                case ExprTypeKind.ARRAY:
                case ExprTypeKind.FUNCTION:
                    return Reg.EAX;

                case ExprTypeKind.CHAR:
                    state.MOV(0, Reg.EAX, Reg.EAX);
                    return Reg.EAX;

                case ExprTypeKind.UCHAR:
                    state.MOV(0, Reg.EAX, Reg.EAX);
                    return Reg.EAX;

                case ExprTypeKind.SHORT:
                    state.MOV(0, Reg.EAX, Reg.EAX);
                    return Reg.EAX;

                case ExprTypeKind.USHORT:
                    state.MOV(0, Reg.EAX, Reg.EAX);
                    return Reg.EAX;

                case ExprTypeKind.LONG:
                case ExprTypeKind.ULONG:
                case ExprTypeKind.POINTER:
                    state.MOV(0, Reg.EAX, Reg.EAX);
                    return Reg.EAX;

                case ExprTypeKind.FLOAT:
                case ExprTypeKind.DOUBLE:
                case ExprTypeKind.STRUCT_OR_UNION:
                    throw new InvalidProgramException("floats and structs are not supported");

                case ExprTypeKind.VOID:
                default:
                    throw new InvalidProgramException();
            }
        }

        public override void CGenAddress(CGenState state) {
            Reg ret = this.Expr.CGenValue(state);
            if (ret != Reg.EAX) {
                throw new InvalidProgramException();
            }
        }
    }
}
