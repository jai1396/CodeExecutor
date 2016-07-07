using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CodeExecutor
{
    class NIAssemblyManager
    {
        public List<Assembly> InputObjectAssemblies { get; private set; }   //Stores assemblies of input array objects
        //Using statements built from namespaces and assemblies are for references
        public List<string> InputObjectNamespaces { get; private set; }     //Stores namespaces 
        public List<Assembly> ExtSetRefAssemblies { get; private set; }     //Stores assemblies set using AddReferences()

        private StringBuilder externalUsings;   //Keeps track of using statements of external assemblies
        private string prevUsings;

        public NIAssemblyManager()
        {
            InputObjectNamespaces = new List<string>();
            InputObjectAssemblies = new List<Assembly>();
            ExtSetRefAssemblies = new List<Assembly>();

            externalUsings = new StringBuilder();
            prevUsings = null;
        }

        public void ClearInpObjData()
        {
            InputObjectAssemblies.Clear();
            InputObjectNamespaces.Clear();
        }

        public string BuildUsings(bool asmNamesSame)
        {
            string CurUsings;
            if (asmNamesSame)
            {
                CurUsings = prevUsings;
            }
            else
            {
                CurUsings = GetCurUsings(InputObjectNamespaces.Distinct());
            }
            prevUsings = CurUsings;
            return externalUsings.ToString() + "\n" + CurUsings;
        }

        private string GetCurUsings(IEnumerable<string> enumerable)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string s in enumerable)
            {
                sb.AppendLine("using " + s + ";");
            }
            return sb.ToString();
        }

        //Looks through the array of inputs
        //Extracts names and types of user defined classes
        //Updates Dictionary<string, Type> ClassNames
        public bool CompareAsmNames(NVPair[] inputs, NVPair[] prevInputs)
        {
            if (inputs == null && prevInputs == null)
            {
                return true;
            }
            else if (inputs == null)
            {
                if (InputObjectAssemblies.Count > 0)
                {
                    ClearInpObjData();
                }
                return false;
            }

            //Generate old list to compare against
            List<string> oldInputObjectNamespaces = new List<string>();
            if (InputObjectAssemblies.Count > 0)
            {
                oldInputObjectNamespaces = InputObjectNamespaces;
            }

            List<Assembly> newInputObjectAssemblies = new List<Assembly>();
            List<string> newInputObjectNamespaces = new List<string>();
            //Build new lists from current inputs to check against
            foreach (var input in inputs)
            {
                var asm = input.Value.GetType().Assembly;
                var asmName = asm.GetName();
                if (!asmName.FullName.ToString().Contains("mscorlib,"))
                {
                    if (!newInputObjectAssemblies.Any(a => a.GetName().FullName == asmName.FullName))
                    {
                        newInputObjectAssemblies.Add(asm);
                    }
                    var inputNamespace = input.Value.GetType().Namespace;
                    if (!newInputObjectNamespaces.Any(a => a == inputNamespace))
                    {
                        newInputObjectNamespaces.Add(input.Value.GetType().Namespace);
                    }
                }
            }            

            //Compares the two lists of namespaces to check for equality
            bool asmNamesSame = oldInputObjectNamespaces.All(newInputObjectNamespaces.Contains) 
                                    && oldInputObjectNamespaces.Count == newInputObjectNamespaces.Count;

            if (asmNamesSame == false)
            {
                InputObjectAssemblies = newInputObjectAssemblies;
                InputObjectNamespaces = newInputObjectNamespaces;
            }

            return asmNamesSame;

        }

        //Stores externally added references
        public void AddExternalReferences(object[] references)
        {
            foreach (Assembly reference in references)
            {
                ExtSetRefAssemblies.Add(reference);
                externalUsings.AppendLine("using " + reference.GetName().Name + ";");
            }
            ExtSetRefAssemblies = ExtSetRefAssemblies.Distinct().ToList();
        }

        //Clears references
        public void ClearExternalReferences()
        {
            ExtSetRefAssemblies.Clear();
            externalUsings.Clear();
        }
    }
}
