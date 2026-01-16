// BAD: Unused using directive
using System.Collections.Generic;
using System.Linq;

// BAD: Block-scoped namespace instead of file-scoped
namespace Common.BadStyle
{
    // BAD: Missing accessibility modifier
    class HiddenClass
    {
        // BAD: Missing accessibility modifier on method
        void DoSomething()
        {
            Console.WriteLine("test");
        }
    }

    public class BadStyleExamples
    {
        public string? Process(object input)
        {
            // BAD: Traditional cast instead of pattern matching
            if (input is string)
            {
                var result = (string)input;
                return result;
            }

            return null;
        }

        public int GetValue(int? nullable)
        {
            // BAD: Not using null coalescing
            if (nullable != null)
            {
                return nullable.Value;
            }
            return 0;
        }
    }
}
