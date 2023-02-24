
using System.Text;

namespace MemoryTest;

class Program
{
    private static readonly List<Mem>[] mem = new List<Mem>[THREADS];
    private static readonly int[] rounds = new int[THREADS];
    private static readonly int[] errors = new int[THREADS];

    // You may want to adjust the following settings:

    private const int THREADS = 8;
    private const int PER_RUN = 262144;

    private const int ARRAYSIZE_START = 0;
    private const int ARRAYSIZE_END = 256;

    // End of user adjustment area.

    private static bool run = true;
    private static bool shutUp;

    private static void Main(string[] args)
    {
        shutUp = args.Length == 1 && args[0].ToLower() == "--shutup";

        if (!shutUp)
        {
            Console.WriteLine("This demo shows a bug in the memory management of .NET 6 when stressing");
            Console.WriteLine("the garbage collector with a mixture of new byte[] and GC.Allocate-");
            Console.WriteLine("UninitializedArray(..., true). We did test this on various hardware but");
            Console.WriteLine("it may be that you can't reproduce the test results with your hardware.");
            Console.WriteLine();
            Console.WriteLine("There are two error patterns: (1) byte[] may be overwritten with others");
            Console.WriteLine("or (2) the CLR (more seldom) may just die.");
            Console.WriteLine();
            Console.WriteLine($"You will require RAM. At least ~{((THREADS + 2L) * PER_RUN * ARRAYSIZE_END) / 1048576} MB!");
            Console.WriteLine();
        }

        for (int thd = 0; thd < THREADS; thd++)
        {
            int thdNo = thd;

            ThreadPool.QueueUserWorkItem(delegate
            {
                Random rng = new Random();

                mem[thdNo] = new List<Mem>();

                byte[] bts;

                while (run)
                {
                    // Here you can setup the test case: Case 1 will (in our tests) never lead to an error while case 2 will only very, very
                    // seldom result in an error. However, case 3 will most likely result into an error within the first 5 seconds (depending
                    // on your hardware).
                    //
                    // Please note that propabilities change, if you change ARRAYSIZE_START and ARRAYSIZE_END. Use this for the original demo:
                    // ARRAYSIZE_START = 0 and ARRAYSIZE_END = 256. (Propability of error on my machine: 90%.)
                    // For chungs requested test case (https://github.com/dotnet/runtime/issues/82548#issuecomment-1442435527) use:
                    // ARRAYSIZE_START = 512 and ARRAYSIZE_END = 768. (Propability of error on my machine: 0%.)
                    // As additional test for chungs requested test case:
                    // ARRAYSIZE_START = 16,32,64,128,256 and ARRAYSIZE_END = 768. (Propability of error on my machine: ~50%.)

                    // Case 1: Only new byte[]:
                    //bts = new byte[rng.Next(256)];

                    // Case 2: Only GC.AllocateUninitializedArray(..., true):
                    //bts = GC.AllocateUninitializedArray<byte>(rng.Next(256), true);

                    // Case 3: Both forms in parallel:
                    if (rng.Next(2) == 0)
                        bts = GC.AllocateUninitializedArray<byte>(rng.Next(ARRAYSIZE_START, ARRAYSIZE_END), true);
                    else
                        bts = new byte[rng.Next(256)];

                    // End of cases.

                    rng.NextBytes(bts);

                    mem[thdNo].Add(new Mem(bts));

                    if (mem[thdNo].Count >= PER_RUN)
                    {
                        foreach (Mem sM in mem[thdNo])
                            if (sM.HasSizeChanged)
                            {
                                errors[thdNo]++;
                                Console.WriteLine($"{sM.ErrorReport}                         ");
                            }

                        mem[thdNo] = new List<Mem>();

                        GC.Collect();

                        rounds[thdNo]++;
                    }
                }

                rounds[thdNo] = -1;
            });
        }

        int minRoundsTaken;

        for (int rounds = 0; someRunning(); rounds++)
        {
            minRoundsTaken = minRounds();

            if (minRoundsTaken == 3 && run)
                run = false;

            Thread.Sleep(500);

            if (!shutUp)
                Console.Write($"(please wait..., {errorCount()} errors in {minRoundsTaken} rounds.)\r");
        }

        Console.WriteLine($"{errorCount()} errors. (thd={THREADS}, run={PER_RUN}, size={ARRAYSIZE_START}-{ARRAYSIZE_END})                                        ");
    }

    private static int errorCount()
    {
        int errors = 0;

        for (int position = 0; position < THREADS; position++)
            errors += Program.errors[position];

        return errors;
    }

    private static int minRounds()
    {
        int minRounds = 3;
        int tRound;

        for (int position = 0; position < THREADS; position++)
        {
            tRound = rounds[position];

            if (tRound >= 0 && tRound < minRounds)
                minRounds = tRound;
        }

        return minRounds;
    }

    private static bool someRunning()
    {
        for (int position = 0; position < THREADS; position++)
            if (rounds[position] != -1)
                return true;

        return false;
    }
}
