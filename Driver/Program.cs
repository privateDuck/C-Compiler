using System;
using System.Linq;

namespace Driver {
    class Program {
        static void Main(string[] args) {
            if (args.Length == 0) {
                String src = @"

void printf(const char *fmt, ...);
int* malloc(int size);

const int y = 5;
int main() {  
    int x[5] = {1,2,3,4,5};
    y = x[3];
}
";
                var compilation = CppAst.CppParser.Parse(src);
                compilation.Functions.ToList().ForEach(f => Console.WriteLine(f));
                Console.WriteLine(compilation.Diagnostics.ToString());
                try
                {
                    Compiler compiler = Compiler.FromSource(src);
                    Console.WriteLine(compiler.Assembly);
                    
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            } else {
                Compiler compiler = Compiler.FromFile(args[0]);
                Console.WriteLine(compiler.Assembly);
            }
        }
    }
}
