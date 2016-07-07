namespace CodeExecutor
{
    public class Globals
    {
        public object[] NI_Input_Object;
        public static string objectArrayName = "NI_Input_Object";
        //public ImmutableArray<object> NI_Input_Object;

        public Globals(params object[] arr)
        {
            NI_Input_Object = arr;
        }
    }
}
