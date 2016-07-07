namespace CodeExecutor
{
    public class NVPair
    {
        public string Name { get; private set; }
        public object Value { get; private set; }

        public NVPair(string name, object value)
        {
            Name = name;
            Value = value;
        }
    }
}
