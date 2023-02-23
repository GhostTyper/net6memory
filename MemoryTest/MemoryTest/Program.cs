
using System.Text;

namespace MemoryTest;

class Program
{
    private static readonly List<Mem>[] mem = new List<Mem>[THREADS];
    private static readonly int[] objects = new int[THREADS];
    private static readonly int[] errors = new int[THREADS];

    private const int THREADS = 8;
    private const int PER_RUN = 262144;

    private static void Main(string[] args)
    {
        Console.WriteLine("This demo shows a bug in the memory management of .NET 6 when stressing");
        Console.WriteLine("the garbage collector with a mixture of new byte[] and GC.Allocate-");
        Console.WriteLine("UninitializedArray(..., true). We did test this on various hardware but");
        Console.WriteLine("it may be that you can't reproduce the test results with your hardware.");
        Console.WriteLine();
        Console.WriteLine("There are two error patterns: (1) byte[] may be overwritten with others");
        Console.WriteLine("or (2) the CLR (more seldom) may just die.");
        Console.WriteLine();
        Console.WriteLine($"You will require RAM. Approx ~{((THREADS + 2) * PER_RUN * 256) / 1048576} MB!");
        Console.WriteLine();

        for (int thd = 0; thd < THREADS; thd++)
        {
            int thdNo = thd;

            ThreadPool.QueueUserWorkItem(delegate
            {
                Random rng = new Random();

                mem[thdNo] = new List<Mem>();

                byte[] bts;

                while (true)
                {
                    // Here you can setup the test case: Case 1 will (in our tests) never lead to an error while case 2 will only very, very
                    // seldom result in an error. However, case 3 will most likely result into an error within the first 5 seconds (depending
                    // on your hardware).

                    // Case 1: Only new byte[]:
                    //bts = new byte[rng.Next(256)];

                    // Case 2: Only GC.AllocateUninitializedArray(..., true):
                    //bts = GC.AllocateUninitializedArray<byte>(rng.Next(256), true);

                    // Case 3: Both forms in parallel:
                    if (rng.Next(2) == 0)
                        bts = GC.AllocateUninitializedArray<byte>(rng.Next(256), true);
                    else
                        bts = new byte[rng.Next(256)];

                    // End of cases.

                    rng.NextBytes(bts);

                    mem[thdNo].Add(new Mem(bts));

                    objects[thdNo]++;

                    if (mem[thdNo].Count >= PER_RUN)
                    {
                        foreach (Mem sM in mem[thdNo])
                            if (sM.HasSizeChanged)
                            {
                                errors[thdNo]++;
                                Console.WriteLine($"{sM.ErrorReport}                         ");
                            }

                        mem[thdNo] = new List<Mem>();
                        objects[thdNo] = 0;

                        GC.Collect();
                    }
                }
            });
        }

        while (true)
        {
            Thread.Sleep(500);
            
            StringBuilder sb = new StringBuilder();

            for (int position = 0; position < THREADS; position++)
            {
                sb.Append((objects[position] * 100 / PER_RUN).ToString("000"));
                sb.Append("% ");

                if (errors[position] > 0)
                {
                    sb.Append("err=");
                    sb.Append(errors[position]);
                    sb.Append(' ');
                }
            }

            sb.Append('\r');

            Console.Write(sb.ToString());
        }
    }
}
