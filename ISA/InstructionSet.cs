using CodeGeneration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C_Compiler.ISA
{
    public enum Instructions
    {
        HLT,
        MOV,
        LEA,
        ADD,
        SUB,
        MUL,
        DIV,
        MOD,
        NOT,
        NEG,
        AND,
        OR,
        XOR,
        CMP,
        TEST,
        PUSH,
        POP,
        CALL,
        LEAVE,
        RET,
        SAR,
        SHR,
        SHL,
        JMP,
        JZ,
        JNZ,
        JL,
        JLE,
        JG,
        JGE,
        JE,
        JNE,
        SETE,
        SETNE,
        SETL,
        SETLE,
        SETG,
        SETGE,
        SETB,
        SETNB,
        SETA,
        SETNA
    }

    public enum AddrModes
    {
        REGISTER,   // Register contains either value or ptr depending on opcode, operand[reg_idx - 16bits]
        PTROFFREG,  // Register contains ptr and offset, operand[offset - 12bits, reg_idx - 4bits]
        IMM,        // operand[value or ptr- 16bits]
    }

    public static class InstructionGenerator
    {
        private static Dictionary<string, uint> registers;
        static InstructionGenerator()
        {
            registers.Add("ax", 0);
            registers.Add("bx", 1);
            registers.Add("cx", 2);
            registers.Add("dx", 3);
            registers.Add("bp", 4);
            registers.Add("sp", 5);
        }
        public static ushort EncodeOPCode(Instructions instr, AddrModes addr1, AddrModes addr2)
        {
            return (ushort)((ushort)instr << 10 | (ushort)addr1 << 8 | (ushort)addr2 << 0);
        }
        public static Instruction MOVRR(ushort reg1, ushort reg2)
        {
            ushort code = EncodeOPCode(Instructions.MOV, AddrModes.REGISTER, AddrModes.REGISTER);
            return new Instruction(code, reg1, reg2);
        }
        public static Instruction MOVMR(ushort offset, ushort reg1, ushort reg2)
        {
            ushort code = EncodeOPCode(Instructions.MOV, AddrModes.PTROFFREG, AddrModes.REGISTER);
            ushort op1 = (ushort)(offset << 12 | reg1);
            return new Instruction(code, op1, reg2);
        }
        public static Instruction MOVRM(ushort reg1, ushort offset, ushort reg2)
        {
            ushort code = EncodeOPCode(Instructions.MOV, AddrModes.REGISTER, AddrModes.PTROFFREG);
            ushort op2 = (ushort)(offset << 12 | reg2);
            return new Instruction(code, reg1, op2);
        }
        public static Instruction MOVMV(ushort offset, ushort reg1, ushort value)
        {
            ushort code = EncodeOPCode(Instructions.MOV, AddrModes.PTROFFREG, AddrModes.IMM);
            ushort op1 = (ushort)(offset << 12 | reg1);
            return new Instruction(code, op1, value);
        }
        public static Instruction MOVVM(ushort reg1, ushort value)
        {
            ushort code = EncodeOPCode(Instructions.MOV, AddrModes.REGISTER, AddrModes.IMM);
            return new Instruction(code, reg1, value);
        }
        public static Instruction LEAR(ushort reg1, ushort offset, ushort reg2) {
            ushort code = EncodeOPCode(Instructions.LEA, AddrModes.REGISTER, AddrModes.PTROFFREG);
            ushort op2 = (ushort)(offset << 12 | reg2);
            return new Instruction (code, reg1, op2);
        }
        public static Instruction LEAV(ushort reg1, ushort value)
        {
            ushort code = EncodeOPCode(Instructions.LEA, AddrModes.REGISTER, AddrModes.IMM);
            return new Instruction(code, reg1, value);
        }
    }

    public readonly struct Instruction(ushort opcode, ushort op1, ushort op2)
    {
        public readonly ushort opcode = opcode;
        public readonly ushort op1 = op1;
        public readonly ushort op2 = op2;
    }
}
