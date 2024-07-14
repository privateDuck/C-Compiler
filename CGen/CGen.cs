using ClangSharp.Interop;
using LexicalAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace CodeGeneration {
    public enum Reg {
        EAX,
        ECX,
        EDX,
        EBX,

        EBP,
        ESP,

        ST0 // unused
    }

    public class ASMProg
    {
        readonly List<LineData> lines = [];
        int lineno = 0;

        public ASMProg()
        {
            lines.Add(new LineData(lineno, LineData.LineType.Instruction, "mov ax, main"));
            lines.Add(new LineData(lineno + 1, LineData.LineType.Instruction, "call main"));
            lineno += 2;
        }

        public void AddInstruction(string str)
        {
            lines.Add(new LineData(lineno, LineData.LineType.Instruction, str));
            lineno++;
        }

        public void AddComment(string str)
        {
            lines.Add(new LineData(lineno, LineData.LineType.Comment, str));
            lineno++;
        }

        public void AddLabel(string str)
        {
            lines.Add(new LineData(lineno, LineData.LineType.Label, str));
            lineno++;
        }

        public void AddDeclaration(string str)
        {
            lines.Add(new LineData(lineno, LineData.LineType.Declaration, str));
            lineno++;
        }

        public void AddEmpty()
        {
            lines.Add(new LineData(lineno, LineData.LineType.Empty, ""));
            lineno++;
        }

        public override string ToString()
        {
            StringWriter writer = new StringWriter();
            AddEmpty();
            RearrangeList(lines);
            writer.WriteLine(".PROGRAM");
            for (int i = 0; i < lines.Count - 1; i++)
            {
                if (lines[i].type == LineData.LineType.Declaration) continue;

                if (lines[i].type == LineData.LineType.Label)
                {
                    string space = new string(' ', 8 - lines[i].line.Length);
                    writer.Write($"{lines[i].line}" + space);
                    writer.WriteLine($"{lines[i+1].line}");
                    i++;
                }
                else if (lines[i].type == LineData.LineType.Comment)
                {
                    writer.WriteLine($"    {lines[i].line}");
                }
                else
                {
                    writer.WriteLine($"        {lines[i].line}");
                }
            }
            writer.WriteLine();
            writer.WriteLine(".DATA");
            writer.WriteLine();
            foreach (var line in lines)
            {
                if (line.type != LineData.LineType.Declaration) continue;
                writer.WriteLine(line.ToString());
            }
            return writer.ToString();
        }

        readonly struct LineData(int lineno, LineData.LineType type, string line)
        {
            public readonly int lineno = lineno;
            public readonly LineType type = type;
            public readonly string line = line;

            public override string ToString()
            {
                return $"{lineno}   {line}";
            }

            public enum LineType
            {
                Empty,
                Label,
                Instruction,
                Comment,
                Declaration
            }
        }

        static void RearrangeList(List<LineData> list)
        {
            for (int i = 0; i < list.Count - 1; i++)
            {
                if (list[i].type == LineData.LineType.Label)
                {
                    int j = i;
                    while (j < list.Count - 1 && (list[j + 1].type != LineData.LineType.Instruction && list[j + 1].type != LineData.LineType.Label))
                    {
                        Swap(list, j, j + 1);
                        j++;
                    }
                }
            }
        }

        static void Swap<T>(List<T> list, int index1, int index2)
        {
            (list[index2], list[index1]) = (list[index1], list[index2]);
        }
    }

    public class CGenState {
        private enum Status {
            NONE,
            TEXT,
            DATA
        }

        public static Dictionary<Reg, String> reg_strs = new Dictionary<Reg, String> {
            [Reg.EAX] = "ax",
            [Reg.ECX] = "cx",
            [Reg.EDX] = "dx",
            [Reg.EBX] = "bx",
            [Reg.EBP] = "bp",
            [Reg.ST0] = "st(0)_ununsed",
            [Reg.ESP] = "sp"/*,
            [Reg.ESI] = "si",
            [Reg.EDI] = "edi_unused",
            [Reg.AL] = "al_ununsed",
            [Reg.AX] = "ax_ununsed",
            [Reg.BL] = "bl_ununsed",
            [Reg.BX] = "bx_ununsed",
            [Reg.CL] = "cl_ununsed",*/
        };

        public static String RegToString(Reg reg) => reg_strs[reg];
        private int charnum = 0;
        private bool isLastLabel = false;
        private string lastLabel = "";

        private void WriteOS(String str) {
            string space = new string(' ', 8 - charnum);
            this.os.Write(space + str);
            charnum = 0;
        }
        private void WriteOSNoTab(String str) {
            charnum = str.Length;
            this.os.Write(str);
        }
        private void WriteLineOSOneTab(String str)
        {
            this.os.WriteLine("    " + str);
        }

        private void WriteLineOS(String str = "") {
            string space = new string(' ', 8 - charnum);
            this.os.WriteLine(space + str);
            charnum = 0;
        }
        private void WriteRODATA(String str) => this.rodata.Write(str);
        private void WriteLineRODATA(String str = "") { 

            this.rodata.WriteLine(str); 
        }

        public CGenState() {
            this.os = new System.IO.StringWriter();
            this.rodata = new System.IO.StringWriter();
            this.asm = new ASMProg();
            //this.WriteLineRODATA(".section .rodata");

            this.rodata_idx = 0;
            this.label_idx = 2;
            this.status = Status.NONE;
            this.label_packs = new Stack<LabelPack>();
            this.return_label = -1;
        }

        public void TEXT() {
            if (this.status != Status.TEXT) {
                //this.WriteLineOS(".text");
                this.status = Status.TEXT;
            }
        }

        public void DATA() {
            if (this.status != Status.DATA) {
                //this.WriteLineOS(".data");
                this.status = Status.DATA;
            }
        }

        public void GLOBL(String name) => this.WriteLineOS(/*$".globl {name}"*/);

        public void LOCAL(String name) => this.WriteLineOS($".local {name}");

        public void ALIGN(Int32 align) => this.WriteLineOS($".align {align}");

        public void COMM(String name, Int32 size, Int32 align) => this.WriteLineOS($".comm {name},{size},{align}");

        public void BYTE(Int32 value) => this.WriteLineOS($".byte {value}");

        public void ZERO(Int32 size) => this.WriteLineOS($".zero {size}");

        public void VALUE(Int32 value) => this.WriteLineOS($".value {value}");

        public void LONG(Int32 value) { 
            this.WriteLineOS($"{value}");
        }

        public void DECLWORD(string name, int value)
        {
            asm.AddDeclaration($"{name}:    {value}");
        }

        public void DECLLOCALWORD(string name, int value)
        {
            asm.AddDeclaration($"{name}:    {value} local");
        }

        public void CGenFuncStart(String name) {
            this.WriteOSNoTab(name + ":");
            this.asm.AddLabel(name + ":");
            PUSH(Reg.EBP);
            MOV(Reg.ESP, Reg.EBP);
            this.StackSize = 0;
        }

        /// <summary>
        /// PUSH: push long into stack.
        /// </summary>
        /// <remarks>
        /// PUSH changes the size of the stack, which should be tracked carefully.
        /// So, PUSH is set private. Consider using <see cref="CGenPushLong"/>
        /// </remarks>
        private void PUSH(String src)
        {
            this.WriteLineOS($"push {src}");
            asm.AddInstruction($"push {src}");
        }

        private void PUSH(Reg src) => PUSH(RegToString(src));

        private void PUSH(Int32 imm) => PUSH($"{imm}");

        /// <summary>
        /// POPL: pop long from stack.
        /// </summary>
        /// <remarks>
        /// POPL changes the size of the stack, which should be tracked carefully.
        /// So, POPL is set private. Consider using <see cref="CGenPopLong"/>
        /// </remarks>
        private void POP(String dst)
        {
            this.WriteLineOS($"pop {dst}");
            asm.AddInstruction($"pop {dst}");
        }

        private void POP(Reg dst) => POP(RegToString(dst));

        /// <summary>
        /// MOVL: move a 2-byte short
        /// </summary>
        public void MOV(String src, String dst)
        {
            this.WriteLineOS($"mov {dst}, {src}");
            asm.AddInstruction($"mov {dst}, {src}");
        }

        public void MOV(String src, Reg dst) => MOV(src, RegToString(dst));

        public void MOV(Int32 imm, String dst) => MOV($"{imm}", dst);

        public void MOV(Int32 imm, Reg dst) => MOV($"{imm}", RegToString(dst));

        public void MOV(Reg src, Reg dst) => MOV(RegToString(src), RegToString(dst));

        public void MOV(Reg src, Int32 offset, Reg dst) => MOV(RegToString(src), $"{offset}[{RegToString(dst)}]");

        public void MOV(Int32 offset, Reg src, Reg dst) => MOV($"{offset}[{RegToString(src)}]", RegToString(dst));

        // LEA
        // ===
        // 
        public void LEA(String addr, String dst, string comment = "") {
            if (comment == "")
            {
                this.WriteLineOS($"lea {dst}, {addr}");
                asm.AddInstruction($"lea {dst}, {addr}");
            }
            else
            {
                this.WriteLineOS($"lea {dst}, {addr} # {comment}");
                asm.AddInstruction($"lea {dst}, {addr} # {comment}");
            }
        }

        public void LEA(String addr, Reg dst, string comment = "") => LEA(addr, RegToString(dst), comment);

        public void LEA(Int32 offset, Reg src, Reg dst, string comment = "") => LEA($"{offset}[{RegToString(src)}]", RegToString(dst), comment);

        // CALL
        // ====
        // 
        public void CALL(String addr) {
            this.WriteLineOS("call " + addr);
            asm.AddInstruction("call " + addr);
        }

        // CGenExpandStack
        // ===============
        // 
        public void CGenExpandStackTo(Int32 size, String comment = "") {
            if (size > this.StackSize) {
                SUBL(size - this.StackSize, RegToString(Reg.ESP), comment);
                this.StackSize = size;
            }
        }

        public void CGenExpandStackBy(Int32 nbytes) {
            this.StackSize += nbytes;
            SUBL(nbytes, Reg.ESP);
        }

        public void CGenExpandStackWithAlignment(Int32 nbytes, Int32 align) {
            nbytes = ABT.Utils.RoundUp(this.StackSize + nbytes, align) - this.StackSize;
            CGenExpandStackBy(nbytes);
        }

        public void CGenForceStackSizeTo(Int32 nbytes, string comment = "") {
            this.StackSize = nbytes;
            LEA(-nbytes, Reg.EBP, Reg.ESP, comment);
        }

        public void CGenShrinkStackBy(Int32 nbytes) {
            this.StackSize -= nbytes;
            ADDL(nbytes, Reg.ESP);
        }

        public void CGenExpandStackByOneWord(String comment = "") {
            this.StackSize += 1;
            SUBL(1, Reg.ESP);
        }

        public void CGenShrinkStackByOneWord(String comment = "") {
            this.StackSize -= 1;
            ADDL(1, Reg.ESP);
        }

        public void LEAVE() {
            //WriteLineOS("leave # pop frame, restore %ebp");
            this.WriteLineOS("leave");
            asm.AddInstruction("leave");
        }

        public void RET() {
            //WriteLineOS("ret # pop old %eip, jump");
            this.WriteLineOS("ret");
            asm.AddInstruction("ret");
        }

        public void NEWLINE() {
            this.WriteLineOS();
            asm.AddEmpty();
        }

        public void COMMENT(String comment) {
            this.WriteLineOSOneTab("# " + comment);
            asm.AddComment("# " + comment);
        }

        /// <summary>
        /// NEG addr: addr = -addr
        /// </summary>
        public void NEG(String addr) {
            this.WriteLineOS($"neg {addr}");
            asm.AddInstruction($"neg {addr}");
        }

        public void NEG(Reg dst) => NEG(RegToString(dst));

        /// <summary>
        /// NOT: bitwise not
        /// </summary>
        public void NOT(String addr) {
            this.WriteLineOS($"not {addr}");
            asm.AddInstruction($"not {addr}");
        }

        public void NOT(Reg dst) => NOT(RegToString(dst));

        /// <summary>
        /// ADDL: add long
        /// </summary>
        public void ADDL(String src, String dest, String comment = "") {
            this.WriteOS($"add {dest}, {src}");
            asm.AddInstruction($"add {dest}, {src}");
            if (comment == "") {
                this.WriteLineOS();
            } else {
                this.WriteLineOSOneTab($" # {comment}");
                asm.AddComment("# " + comment);
            }
        }

        public void ADDL(Int32 value, Reg dest, String comment = "") => ADDL($"{value}", RegToString(dest), comment);

        public void ADDL(Reg er, Reg ee, String comment = "") => ADDL(RegToString(er), RegToString(ee), comment);

        /// <summary>
        /// SUBL: subtract long
        /// </summary>
        public void SUBL(String src, String dest, String comment = "") {
            this.WriteOS($"sub {dest},  {src}");
            asm.AddInstruction($"sub {dest}, {src}");
            if (comment == "") {
                this.WriteLineOS();
            } else {
                this.WriteLineOSOneTab(" # " + comment);
                asm.AddComment("# " + comment);
            }
        }

        private void SUBL(Int32 er, String ee, String comment = "") => SUBL($"{er}", ee, comment);

        public void SUBL(Int32 er, Reg ee, String comment = "") => SUBL($"{er}", RegToString(ee), comment);

        public void SUBL(Reg er, Reg ee, String comment = "") => SUBL(RegToString(er), RegToString(ee), comment);

        public override String ToString() {
            return asm.ToString();//this.os.ToString() + this.rodata;
        }

        /// <summary>
        /// ANDL er, ee
        /// ee = er & ee
        /// </summary>
        public void ANDL(String src, String dest)
        {
            this.WriteLineOS($"and {dest},  {src}");
            asm.AddInstruction($"and {dest},  {src}");
        }

        public void ANDL(Reg er, Reg ee) => ANDL(RegToString(er), RegToString(ee));

        public void ANDL(Int32 er, Reg ee) => ANDL($"{er}", RegToString(ee));

        /// <summary>
        /// ORL er, ee
        ///     ee = ee | er
        /// </summary>
        public void ORL(String src, String dest, String comment = "") {
            this.WriteOS($"or {dest},  {src}");
            asm.AddInstruction($"or {dest},  {src}");
            if (comment == "") {
                this.WriteLineOS();
            } else {
                this.WriteLineOSOneTab(" # " + comment);
                asm.AddComment("# " + comment);
            }
        }

        public void ORL(Reg er, Reg ee, String comment = "") {
            ORL(RegToString(er), RegToString(ee), comment);
        }

        /// <summary>
        /// SALL er, ee
        /// ee = ee << er
        /// Note that there is only one Kind of lshift.
        /// </summary>
        public void SALL(String shift, String operand) {
            this.WriteLineOS($"shl {operand}, { shift}");
            asm.AddInstruction($"shl {operand}, {shift}");
        }

        public void SALL(Reg er, Reg ee) {
            SALL(RegToString(er), RegToString(ee));
        }

        /// <summary>
        /// SARL er, ee (arithmetic shift)
        /// ee = ee >> er (append sign bit)
        /// </summary>
        public void SARL(String shift, String operand) {
            this.WriteLineOS($"sar {operand}, {shift}");
            asm.AddInstruction($"sar {operand}, {shift}");
        }

        public void SARL(Reg er, Reg ee) => SARL(RegToString(er), RegToString(ee));

        /// <summary>
        /// SHRL er, ee (logical shift)
        /// ee = ee >> er (append 0)
        /// </summary>
        public void SHRL(String shift, String operand) {
            this.WriteLineOS($"shr {operand}, {shift}");
            asm.AddInstruction("shr " + operand + ", " + shift);
        }

        public void SHRL(Reg er, Reg ee) => SHRL(RegToString(er), RegToString(ee));

        public void SHRL(Int32 er, Reg ee) => SHRL($"{er}", RegToString(ee));

        /// <summary>
        /// XORL er, ee
        /// ee = ee ^ er
        /// </summary>
        public void XORL(String src, String dest) {
            this.WriteLineOS($"xor {dest},  {src}");
            asm.AddInstruction($"xor {dest},  {src}");
        }

        public void XORL(Reg er, Reg ee) {
            XORL(RegToString(er), RegToString(ee));
        }

        /// <summary>
        /// IMUL: signed multiplication. %edx:%eax = %eax * {addr}.
        /// </summary>
/*        public void IMUL(String addr) {
            this.WriteLineOS($"imul {addr}");
        }

        public void IMUL(Reg er) {
            IMUL(RegToString(er));
        }
*/

        /// <summary>
        /// MUL: unsigned multiplication. %edx:%eax = %eax * {addr}.
        /// </summary>
        public void MUL(String addr) {
            this.WriteLineOS($"mul {addr}");
            asm.AddInstruction($"mul {addr}");
        }

        public void MUL(Reg er) {
            MUL(RegToString(er));
        }

        /// <summary>
        /// CLTD: used before division. clear %edx.
        /// </summary>
        //public void CLTD() => this.WriteLineOS("cltd");

        /// <summary>
        /// IDIVL: signed division. %eax = %edx:%eax / {addr}.
        /// </summary>
/*        public void IDIVL(String addr) {
            this.WriteLineOS($"idivl {addr}");
        }

        public void IDIVL(Reg er) => IDIVL(RegToString(er));
*/

        /// <summary>
        /// IDIVL: unsigned division. %eax = %edx:%eax / {addr}.
        /// </summary>
        public void DIVL(String addr) {
            this.WriteLineOS($"div {addr}");
            asm.AddInstruction($"div {addr}");
        }

        public void DIVL(Reg er) => DIVL(RegToString(er));

        /// <summary>
        /// CMPL: compare based on subtraction.
        /// Note that the order is reversed, i.e. ee comp er.
        /// </summary>
        public void CMPL(String right, String left) {
            this.WriteLineOS($"cmp {left}, {right}");
            asm.AddInstruction($"cmp {left}, {right}");
        }

        public void CMPL(Reg er, Reg ee) => CMPL(RegToString(er), RegToString(ee));

        public void CMPL(Int32 imm, Reg ee) => CMPL($"{imm}", RegToString(ee));

        /// <summary>
        /// TEST: used like test ax, ax: compare ax with zero.
        /// </summary>
        public void TESTL(String right, String left)
        {
            this.WriteLineOS($"test {left}, {right}");
            asm.AddInstruction($"test {left}, {right}");
        }

        public void TESTL(Reg er, Reg ee) => TESTL(RegToString(er), RegToString(ee));

        /// <summary>
        /// SETE: set if equal to.
        /// </summary>
        public void SETE(String dst)
        {
            this.WriteLineOS($"sete {dst}");
            asm.AddInstruction($"sete {dst}");
        }

        public void SETE(Reg dst) => SETE(RegToString(dst));

        /// <summary>
        /// SETNE: set if not equal to.
        /// </summary>
        public void SETNE(String dst)
        {
            this.WriteLineOS($"setne {dst}");
            asm.AddInstruction($"setne {dst}");
        }
        public void SETNE(Reg dst) => SETNE(RegToString(dst));

        /// <summary>
        /// SETG: set if greater than (signed).
        /// </summary>
        public void SETG(String dst) {
            this.WriteLineOS($"setg {dst}");
            asm.AddInstruction($"setg {dst}");
        }

        public void SETG(Reg dst) => SETG(RegToString(dst));

        /// <summary>
        /// SETGE: set if greater or equal to (signed).
        /// </summary>
        public void SETGE(String dst) {
            this.WriteLineOS($"setge {dst}");
            asm.AddInstruction($"setge {dst}");
        }

        public void SETGE(Reg dst) => SETGE(RegToString(dst));

        /// <summary>
        /// SETL: set if less than (signed).
        /// </summary>
        public void SETL(String dst) {
            this.WriteLineOS($"setl {dst}");
            asm.AddInstruction($"setl {dst}");
        }

        public void SETL(Reg dst) => SETL(RegToString(dst));

        /// <summary>
        /// SETLE: set if less than or equal to (signed).
        /// </summary>
        public void SETLE(String dst) {
            this.WriteLineOS($"setle {dst}");
            asm.AddInstruction($"setle {dst}");
        }

        public void SETLE(Reg dst) => SETLE(RegToString(dst));

        /// <summary>
        /// SETB: set if below (unsigned).
        /// </summary>
        public void SETB(String dst) {
            this.WriteLineOS($"setb {dst}");
            asm.AddInstruction($"setb {dst}");
        }

        public void SETB(Reg dst) => SETB(RegToString(dst));

        /// <summary>
        /// SETNB: set if not below (unsigned).
        /// </summary>
        public void SETNB(String dst) {
            this.WriteLineOS($"setnb {dst}");
            asm.AddInstruction($"setnb {dst}");
        }

        public void SETNB(Reg dst) => SETNB(RegToString(dst));

        /// <summary>
        /// SETA: set if above (unsigned).
        /// </summary>
        public void SETA(String dst) {
            this.WriteLineOS($"seta {dst}");
            asm.AddInstruction($"seta {dst}");
        }

        public void SETA(Reg dst) => SETA(RegToString(dst));

        /// <summary>
        /// SETNA: set if not above (unsigned).
        /// </summary>
        public void SETNA(String dst) {
            this.WriteLineOS($"setna {dst}");
            asm.AddInstruction($"setna {dst}");
        }

        public void SETNA(Reg dst) => SETNA(RegToString(dst));

        public void JMP(Int32 label)
        {
            this.WriteLineOS($"jmp .L{label}");
            asm.AddInstruction($"jmp L{label}");
        }

        public void JZ(Int32 label)
        {
            this.WriteLineOS($"jz .L{label}");
            asm.AddInstruction($"jz L{label}");
        }

        public void JNZ(Int32 label)
        {
            this.WriteLineOS($"jz .L{label}");
            asm.AddInstruction($"jz L{label}");
        }

        public void JC(int label) => this.WriteLineOS($"jc .L{label}");
        public void JE(int label) => this.WriteLineOS($"je .L{label}");
        public void JNE(int label) => this.WriteLineOS($"jne .L{label}");
        public void JL(int label) => this.WriteLineOS($"jl .L{label}");
        public void JLE(int label) => this.WriteLineOS($"jle .L{label}"); 
        public void JG(int label) => this.WriteLineOS($"jg .L{label}");
        public void JGE(int label) => this.WriteLineOS($"jge .L{label}");

        public Int32 CGenPushLong(Reg src) {
            PUSH(src);
            this.StackSize += 1;
            return this.StackSize;
        }

        public Int32 CGenPushLong(Int32 imm) {
            PUSH(imm);
            this.StackSize += 1;
            return this.StackSize;
        }

        public void CGenPopLong(Int32 saved_size, Reg dst) {
            if (this.StackSize == saved_size) {
                POP(dst);
                this.StackSize -= 1;
            } else {
                MOV(-saved_size, Reg.EBP, dst);
            }
        }

        /// <summary>
        /// Fast Memory Copy using assembly.
        /// Make sure that
        /// 1) %esi = source address
        /// 2) %edi = destination address
        /// 3) %ecx = number of bytes
        /// </summary>
        public void CGenMemCpy() {
            SUBL(1, Reg.ECX);
            int start = RequestLabel();
            int end = RequestLabel();
            /*JZ()
            MOVB(Reg.CL, Reg.AL);
            SHRL(2, Reg.ECX);
            CLD();
            this.os.WriteLine("    rep movsl");
            MOVB(Reg.AL, Reg.CL);
            ANDB(3, Reg.CL);
            this.os.WriteLine("    rep movsb");*/
        }

        /// <summary>
        /// Fast Memory Copy using assembly.
        /// Make sure that
        /// 1) %esi = source address
        /// 2) %edi = destination address
        /// 3) %ecx = number of bytes
        /// </summary>
        public void CGenMemCpyReversed() {
            
        }

        public String CGenLongConst(Int32 val) {
            String name = "LC" + this.rodata_idx;
            //this.WriteLineRODATA(".align 4");
            this.WriteRODATA(name + ": ");
            this.WriteLineRODATA(val.ToString());
            this.rodata_idx++;
            asm.AddDeclaration($"{name}:    {val}");
            return name;
        }

        public String CGenString(String str) {
            String name = "LC" + this.rodata_idx;
            this.WriteRODATA(name + ": ");
            this.WriteLineRODATA("\"" + str + "\"");
            this.rodata_idx++;
            asm.AddDeclaration($"{name}:    \"{str}\"");
            return name;
        }

        public void CGenLabel(String label)
        {
            isLastLabel = true;
            lastLabel = label;
            this.WriteOSNoTab($".{label}:");
            asm.AddLabel($"{label}:");
        }

        public void CGenLabel(Int32 label) => CGenLabel($"L{label}");

        private readonly StringWriter os;
        private readonly StringWriter rodata;
        public readonly ASMProg asm;

        private Int32 rodata_idx;
        public Int32 label_idx;

        private Status status;

        public Int32 StackSize { get; private set; }

        public Int32 RequestLabel() {
            return this.label_idx++;
        }


        //private Stack<Int32> _continue_labels;
        //private Stack<Int32> _break_labels;

        private struct LabelPack {
            public LabelPack(Int32 continue_label, Int32 break_label, Int32 default_label, Dictionary<Int32, Int32> value_to_label) {
                this.continue_label = continue_label;
                this.break_label = break_label;
                this.default_label = default_label;
                this.value_to_label = value_to_label;
            }
            public readonly Int32 continue_label;
            public readonly Int32 break_label;
            public readonly Int32 default_label;
            public readonly Dictionary<Int32, Int32> value_to_label;
        }

        private readonly Stack<LabelPack> label_packs;

        public Int32 ContinueLabel => this.label_packs.First(_ => _.continue_label != -1).continue_label;

        public Int32 BreakLabel => this.label_packs.First(_ => _.break_label != -1).break_label;

        public Int32 DefaultLabel {
            get {
                Int32 ret = this.label_packs.First().default_label;
                if (ret == -1) {
                    throw new InvalidOperationException("Not in a switch statement.");
                }
                return ret;
            }
        }

        public Int32 CaseLabel(Int32 value) => this.label_packs.First(_ => _.value_to_label != null).value_to_label[value];
        // label_packs.First().value_to_label[Value];

        public void InLoop(Int32 continue_label, Int32 break_label) {
            this.label_packs.Push(new LabelPack(continue_label, break_label, -1, null));
            //_continue_labels.Push(continue_label);
            //_break_labels.Push(break_label);
        }

        public void InSwitch(Int32 break_label, Int32 default_label, Dictionary<Int32, Int32> value_to_label) {
            this.label_packs.Push(new LabelPack(-1, break_label, default_label, value_to_label));
        }

        public void OutLabels() {
            this.label_packs.Pop();
            //_continue_labels.Pop();
            //_break_labels.Pop();
        }

        private readonly Dictionary<String, Int32> _goto_labels = new Dictionary<String, Int32>();

        public Int32 GotoLabel(String label) {
            return this._goto_labels[label];
        }

        private Int32 return_label;
        public Int32 ReturnLabel {
            get {
                if (this.return_label == -1) {
                    throw new InvalidOperationException("Not inside a function.");
                }
                return this.return_label;
            }
        }

        public void InFunction(IReadOnlyList<String> goto_labels) {
            this.return_label = RequestLabel();
            this._goto_labels.Clear();
            foreach (String goto_label in goto_labels) {
                this._goto_labels.Add(goto_label, RequestLabel());
            }
        }

        public void OutFunction() {
            this.return_label = -1;
            this._goto_labels.Clear();
        }

    }
}