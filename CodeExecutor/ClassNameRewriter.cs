using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeExecutor
{
    //NOT USED IN PROJECT 2 as using statements were added instead of modifying class names
    class ClassNameRewriter : CSharpSyntaxRewriter
    {
        private Dictionary<string, Type> ClassNames;
        private List<string> CNamespaces;//fullnames of types found in input objects
        private List<string> ExtRefs;//fullnames of types found in externally referenced assemblies
        private bool decsNeedToBeChanged;
        private int currentObjectIndex;
        private string objectArrayName;

        public ClassNameRewriter(Dictionary<string, Type> _classNames, List<string> _extRefs) : base() //gets dictionary with <name, fullname>
        {
            decsNeedToBeChanged = true;     //Indicates whether there are remaining lines to modify
            currentObjectIndex = 0;         //Index of object to be modified now
            ClassNames = _classNames;
            CNamespaces = new List<string>();
            objectArrayName = Globals.objectArrayName;
            foreach (var key in ClassNames.Keys)
            {   //Populates a list with fullnames of classes eg (NameSpace1.NameSpace2.X) generated from input objects
                CNamespaces.Add(key);
            }
            ExtRefs = _extRefs; //contains fullnames of classes from externally set references using AddReferences()
        }

        public override SyntaxNode VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            if (decsNeedToBeChanged==false)
            {
                //return base.VisitVariableDeclaration(node);
                var firstDescNode = node.DescendantNodes().First();
                if (firstDescNode is PredefinedTypeSyntax)
                {
                    return base.VisitVariableDeclaration(node);
                }
                //Console.WriteLine("boo");
                SyntaxNode userDefNode = base.VisitVariableDeclaration(node);
                //var declarationID = firstDescNode as IdentifierNameSyntax;
                //declarationID = ChangeUserDefinedClassNames(declarationID);
                userDefNode = ChangeUserDefinedClassNames(userDefNode);
                return userDefNode;
            }

            SyntaxNode updatedNode = base.VisitVariableDeclaration(node);

            if (node.DescendantNodes().OfType<EqualsValueClauseSyntax>().Count()==0)
            {//No assignment in variable declaration
                if(node.DescendantNodes().OfType<PredefinedTypeSyntax>().Count()==0)
                {//Declared variable not of a predefined type eg int float bool etc
                    //Console.Write("UserDef: ");                   

                    updatedNode = ChangeUserDefinedClassNames(updatedNode);  //Changes all class names to include namespace

                    
                }
                //Converts for example 'int i' to 'int i = (int)NI_Input_Object[0]'
                updatedNode = PerformCastingAndAssignment(updatedNode);
            }
            else
            {
                //Found a declaration of the form Type name = value; etc
                //Indicates that wired inputs have all been declared 
                decsNeedToBeChanged = false;
                return VisitVariableDeclaration(node);
            }
            //Console.WriteLine(updatedNode.ToFullString());


            return updatedNode;
        }

        private SyntaxNode ChangeUserDefinedClassNames(SyntaxNode updatedNode)
        {
            //get qualifiedname nodes that are not descended from qualifiednames
            //using where clause
            //and get identifier name nodes that are not descendents of qualnames
            //use dictionary of namesyntax type

            var qualifiedIdentifiers = updatedNode.DescendantNodes().OfType<QualifiedNameSyntax>()
                                            .Where(node => node.Ancestors().OfType<QualifiedNameSyntax>().Count() == 0);

            var classIdentifiers = updatedNode.DescendantNodes().OfType<IdentifierNameSyntax>()
                                            .Where(node => node.Ancestors().OfType<QualifiedNameSyntax>().Count() == 0);

            var changedNodeDict = new Dictionary<NameSyntax, NameSyntax>();

            string fullName;
            foreach (var qualID in qualifiedIdentifiers)
            {
                fullName = MatchGivenNameWithRefs(qualID.ToString());
                if (fullName == null)
                {
                    //throw "not found";
                }
                else
                {
                    var newNode = SyntaxFactory.IdentifierName(fullName)
                                .WithLeadingTrivia(qualID.GetLeadingTrivia())
                                .WithTrailingTrivia(qualID.GetTrailingTrivia())
                                ;
                    changedNodeDict.Add(qualID, newNode);
                }
            }

            foreach (var classIdentifier in classIdentifiers)
            {
                fullName = MatchGivenNameWithRefs(classIdentifier.Identifier.Text);
                if (fullName == null)
                {
                    //throw "not found";
                }
                else
                {
                    SyntaxToken updatedIdName = SyntaxFactory.Identifier(
                            classIdentifier.Identifier.LeadingTrivia,
                            fullName,
                            classIdentifier.Identifier.TrailingTrivia
                            );
                    var newclassIdentifier = classIdentifier.WithIdentifier(updatedIdName);

                    changedNodeDict.Add(classIdentifier, newclassIdentifier);
                }
            }

            updatedNode = updatedNode.ReplaceNodes(changedNodeDict.Keys.AsEnumerable(), (n1, n2) =>
            { return changedNodeDict[n1]; });

            
           
            return updatedNode;
        }

        //First searches input objects and returns in a match is found
        //If no match among CNamespaces then it checks ExtRefs
        private string MatchGivenNameWithRefs(string givenName)
        {
            string fullName = CNamespaces.SingleOrDefault(str =>
            {
                var index = str.LastIndexOf(givenName);
                if (index!=-1 && index == str.Length - givenName.Length)
                {
                    return true;
                }
                return false;
            });
            if (fullName != null) 
            {
                return fullName;
            }

            fullName = ExtRefs.SingleOrDefault(str =>
            {
                var index = str.LastIndexOf(givenName);
                if (index != -1 && index == str.Length - givenName.Length)
                {
                    return true;
                }
                return false;
            });
            if (fullName != null) 
            {
                return fullName;
            }
            return null;
        }

        private SyntaxNode PerformCastingAndAssignment(SyntaxNode updatedNode)
        {            
            string fullIdentifierName = "";
            var variableTypeDefNode = updatedNode.DescendantNodes().First();

            if(variableTypeDefNode is PredefinedTypeSyntax)
            {
                fullIdentifierName = ((PredefinedTypeSyntax)variableTypeDefNode).Keyword.Text;
            }
            else if(variableTypeDefNode is IdentifierNameSyntax)
            {
                fullIdentifierName = ((IdentifierNameSyntax)variableTypeDefNode).Identifier.Text;
            }

            var declarator = updatedNode.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single();
            var oldDeclarator = declarator;

            string triviaText = " = (" + fullIdentifierName + ")" + objectArrayName + "[" + currentObjectIndex + "]";
            currentObjectIndex++;

            var trivia = SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, triviaText);
            declarator = declarator.WithTrailingTrivia(trivia);


            return updatedNode.ReplaceNode(oldDeclarator, declarator);
        }

    }
}
