using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;

namespace CodeExecutor
{
    public class ExecutionUnit
    {
        private string prevCode;
        private string prevEditedCode;
        private NVPair[] prevInputs;
        public object Result { get; private set; }

        private NIAssemblyManager asmManager;
        
        private SyntaxTree curTree;
        private ScriptOptions scriptOptions;

        private string prevDeclarations;

        private bool referencesChanged;

        public ExecutionUnit()
        {
            prevInputs = null;
            Result = null;
            curTree = null;
            scriptOptions = DefaultScriptOptions();
            asmManager = new NIAssemblyManager();
            prevDeclarations = null;
            referencesChanged = false;
        }

        // * Assumptions:
        // * 
        // 1 All declarations of wired inputs happen at top of code segment eg (for only project 1)
        // * int i;
        // * int b;
        // * string s;
        // * Rat r;
        // * <other code>
        // * 
        // * For project 2, no declaration of any wired inputs in code.
        // * All objects are passed as a name value pair.
        // * 
        // 2 User defined classes do not have System in namespace name
        // * 
        // 3 Wired inputs cannot be declared using var (for only project 1)
        // */
        // 1. Explicitly set references(using strong name or path)
        //allow publicly accessible method for references and imports to be set
        //    could accept assemblies or strings
        // 2. Support for expressions without declarations : x.Where( y => y.Text == inputText)
        //do in separate project
        //    get a <name,object> pair and replace their values accordingly to evaluate the expression
        // 3. Get all variable declarations in expressions like the one above.
        //no objects passed
        //    syntactical analysis the main aim
        //    have to try and find out which are the identifiers that would be passed as wired inputs

        public async void Execute(string code, params NVPair[] inputs)
        {
            //Checks whether namespaces of input objects have changed from previous execution
            bool asmNamesSame = asmManager.CompareAsmNames(inputs, prevInputs);
            
            string CurCode;
            if (asmNamesSame && AreCodesEqual(code, prevCode))
            {
                //No need for editing code in this case
                CurCode = prevEditedCode;
            }
            else
            {
                CurCode = EditCode(code);
                if(!asmNamesSame)
                {
                    scriptOptions = ModifyScriptOptions(scriptOptions);
                }
            }

            if (referencesChanged)
            {//If external references have been set since the last execution
                scriptOptions = ModifyScriptOptions(scriptOptions);
                referencesChanged = false;
            }

            var globals = BuildGlobals(inputs); //Inputs passed to script execution state as globals

            string Usings = asmManager.BuildUsings(asmNamesSame); //Necessary using statements

            string Declarations; //Builds the declarations of wired inputs           
            if (inputs!=null && prevInputs != null)
            {
                if (Enumerable.SequenceEqual(inputs, prevInputs))
                {
                    Declarations = prevDeclarations;
                }
                else
                {
                    Declarations = BuildDeclarations(inputs);
                }
            }
            else
            {
                if (inputs!=null)
                {
                    Declarations = BuildDeclarations(inputs);
                }
                else
                {
                    Declarations = "";
                }
            }

            Console.WriteLine(Usings + Declarations + CurCode);//comment out

            var state = await CSharpScript.RunAsync(Usings + Declarations + CurCode, scriptOptions, globals: globals);
            var result = state.ReturnValue;

            Result = result;
            prevEditedCode = CurCode;
            prevCode = code;
            prevInputs = inputs;
            prevDeclarations = Declarations;
        }

        private string BuildDeclarations(NVPair[] inputs)
        {
            string referenceName = Globals.objectArrayName;
            StringBuilder decs = new StringBuilder();
            for (int i = 0; i < inputs.Length; i++)
            {
                NVPair input = inputs[i];
                object val = input.Value;
                string type = val.GetType().Name;
                string name = input.Name;
                string typecast = "(" + type + ")";
                string lhs = type + " " + name;
                string rhs = typecast + referenceName + "[" + i.ToString() + "];";
                decs.AppendLine(lhs + " = " + rhs);
            }
            return decs.ToString();
        }

        private Globals BuildGlobals(params NVPair[] inputs)
        {
            object[] globalInputs = null;
            if (inputs!=null)
            {
                globalInputs = inputs.Select(i => i.Value).ToArray();
            }            
            return new Globals(globalInputs);
        }

        private ScriptOptions DefaultScriptOptions()
        {
            return ScriptOptions.Default
                                    .WithImports("System.IO", "System");
        }

        private ScriptOptions ModifyScriptOptions(ScriptOptions scriptOptions)
        {
            //Method 1: Simple addition to scriptOptions
            //Problem is it can have multiple copies of same reference stored as members of the
            //MetadataReferences property
            //return scriptOptions.AddReferences(asmManager.InputObjectAssemblies.ToArray());


            //Method 2: Query whether assembly file location already exists in
            //MetadataReferences property

            var references = scriptOptions.MetadataReferences;
            var locations = references.Select(i => i.Display);
            var asmList = asmManager.InputObjectAssemblies;
            var asmDict = new Dictionary<Assembly, string>();
            foreach (var asm in asmList)
            {
                asmDict.Add(asm, asm.Location);
            }
            var removeList = new List<Assembly>();
            foreach (var key in asmDict.Keys)
            {
                if (locations.Contains(asmDict[key]))
                {
                    removeList.Add(key);
                }
            }
            removeList = removeList.Distinct().ToList();
            foreach (var key in removeList)
            {
                asmDict.Remove(key);
            }
            return scriptOptions.AddReferences(asmDict.Keys);
        }

        //Not implemented as no editing of code was required in project 2
        private string EditCode(string code)
        {
            /*string beginClassWrapper = @"class Program{
    public static void Main()
    {
        ";
            string endClassWrapper = @"
    }
}";
            var tree = SyntaxFactory.ParseSyntaxTree(beginClassWrapper + code + endClassWrapper);
            ClassNameRewriter classNameRewriter = new ClassNameRewriter(AsmNames, ExtSetRefAssemblies);
            var newRoot = classNameRewriter.Visit(tree.GetRoot());

            var tempCode = newRoot.ToFullString();
            var newCode = tempCode.Substring(beginClassWrapper.Length, tempCode.Length - beginClassWrapper.Length - endClassWrapper.Length);

            return newCode;*/
            return code;
        }


        //Generates syntax trees for current and previous codes
        //Compares them to test for equality
        private bool AreCodesEqual(string code, string prevCode)
        {
            if(prevCode == null || prevCode == "")
            {
                if(code == null || code == "")
                {
                    return true;
                }
                else
                {
                    curTree = SyntaxFactory.ParseSyntaxTree(code); 
                    return false;
                }
            }
            var tree = SyntaxFactory.ParseSyntaxTree(code);
            if(tree.IsEquivalentTo(curTree))
            {
                return true;
            }
            else
            {
                curTree = tree;
                return false;
            }
        }

        //Fullnames of referenced assemblies must be stored to compare against
        private void AddReferences(object[] references)
        {
            if(references.Length>0)
            {
                references = references.Distinct().ToArray();
                if (references[0] is Assembly)
                {
                    scriptOptions = scriptOptions.AddReferences(Array.ConvertAll(references, item => (Assembly)item));
                    asmManager.AddExternalReferences(references);
                }
            }
        }

        public void AddReferences(params Assembly[] references) => AddReferences((object[])references);
        public void AddReferences(IEnumerable<Assembly> references) => AddReferences((object[])references.ToArray());

        public void ClearReferences()
        {
            scriptOptions = DefaultScriptOptions();
            asmManager.ClearExternalReferences();
            referencesChanged = true;
        }

        //To add specific namespaces within assemblies than must be used
        public void AddNameSpaceImports(params string[] imports)
        {
            scriptOptions = scriptOptions.AddImports(imports);
        }

        
    }
}
