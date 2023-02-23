namespace MemoryTest
{
    struct Mem
    {
        public readonly byte[] Bytes;
        public readonly int Length;

        public Mem(byte[] bytes)
        {
            Bytes = bytes;
            Length = Bytes.Length;
        }

        public bool HasSizeChanged => Bytes is null || Bytes.Length != Length;

        public string ErrorReport => $"Array size changed from {Length} to {(Bytes ?? new byte[0]).Length}.";
    }
}
