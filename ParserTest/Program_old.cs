using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using java.io;
using java.util;
using edu.stanford.nlp.simple;
using edu.stanford.nlp.process;
using edu.stanford.nlp.trees;
using edu.stanford.nlp.parser.lexparser;


using Console = System.Console;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DescriptionParser
{
    public class Data
    {
        public string text { get; set; }
        public Ano annotation { get; set; }
    }

    public class Ano
    {
        public string intent { get; set; }
        public List<Entity> entities { get; set; }
        public List<Entity> features { get; set; }
    }

    public class Entity
    {
        public string start { get; set; }
        public string length { get; set; }
        public string label { get; set; }
    }

    public class Description
    {
        static Dictionary<string, string> dict = new Dictionary<string, string>();
        static string jarRoot;
        static string modelsDirectory;

        static void Main(string[] args)
        {
            Init();

            // This option shows loading and using an explicit tokenizer
            //var sent = "70% of the World's Population now own a mobile phone."; var percentage = "70%";
            //var sent = "70% of hiring managers said it's more important than IQ"; var percentage = "16%";
            //var sent = "less than 1% of US men knew how to tie a bow tie."; var percentage = "1%";
            //var sent = "8 in 10 small business owners applied for some form of financing in 2015"; var percentage = "8 in 10";
            //var sent = "People will read about 20% of the text on the average web page."; var percentage = "20%";
            //var sent = "62% are familiar with alternative business loans."; var percentage = "16%";
            //var sent = "300 thousand seabirds are killed each year."; var percentage = "16%";
            //var sent = "40% from defense, 60% from nondefense programs."; var percentage = "16%";
            //var sent = "12% of e-mail users have actually tried to buy stuff from spam"; var percentage = "16%";
            //var sent = "1/3rd of recent college grads have delayed purchasing a house or a car because of their debt."; var percentage = "1/3rd";
            //var sent = "Less than 2% of our planets ocean is protected from factors that kill marine life and destroy environments."; var percentage = "16%";
            //var sent = "45% -- the employment rate of dropouts"; var percentage = "16%";
            //var sent = "Young children who watch sesame street had 16% higher GPAs in high school."; var percentage = "16%";
            //var sent = "Last month, 206 D.C. public school teachers were fired for poor performance under IMPACT, amounting to 5 percent of the 4,100 teachers in the city school system."; var percentage = "5 percent";
            var sent = "Suppose that a program spends 60% of its time in I/O operations, pre and post-processing"; var percentage = "60%";


            //for debug
            List<string> ls = getDescription(sent, percentage);
            Console.WriteLine("{0}\n", sent);
            foreach (string s in ls)
            {
                if (s != null)
                    Console.WriteLine(s);
                else
                    Console.WriteLine("Error: Fail to parse the sentence");
            }
            Console.WriteLine("\n");
            Console.ReadLine();


            //for json
            //string sent = null;
            //string percentage = null;
            //string json = System.IO.File.ReadAllText("D:\\MSRA\\Txt2Vis\\Parser\\GetDescription\\ParserTest\\PercentText_label_2_test62.json");
            ////string json = System.IO.File.ReadAllText("D:\\MSRA\\Txt2Vis\\Parser\\GetDescription\\PercentText_label_2_train538.json");
            //JsonSerializer serializer = new JsonSerializer();
            //var text = JsonConvert.DeserializeObject<List<Data>>(json);
            //foreach (var item in text)
            //{
            //    sent = item.text;
            //    foreach (var entity in item.annotation.features)
            //    {
            //        if (entity.label == "Percentage")
            //        {
            //            percentage = item.text.Substring(Int32.Parse(entity.start), Int32.Parse(entity.length));
            //            Console.WriteLine(percentage);
            //        }
            //    }

            //    Console.WriteLine("{0}\n", sent);
            //    List<string> ls = getDescription(sent, percentage);

            //    //print result
            //    foreach (string s in ls)
            //    {

            //        if (s != null)
            //            Console.WriteLine(s);
            //        else
            //            Console.WriteLine("Error: Fail to parse the sentence");
            //    }
            //    Console.WriteLine("\n");
                //Console.ReadLine();
            //}





            Console.ReadLine();
        }

        public static List<string> getDescription(string sent, string percentage)
        {
            //set up environment
            //Init();

            //use Stanford.NLP.Net to parse the sentence
            Tree tree = Parse(sent);

            //calculate the two types of description
            List<string> ls = new List<string>();
            removePercentage(tree, percentage, ls);

            return ls;
        }

        //set up environment
        public static void Init()
        {
            //load json to create dictionary
            string json = System.IO.File.ReadAllText("..\\..\\verbs-dictionaries.json");
            JsonSerializer serializer = new JsonSerializer();
            JArray values = JsonConvert.DeserializeObject<JArray>(json);
            foreach (var item in values.Children())
            {
                dict.Add((string)item[0], (string)item[3]);
            }

            // Path to models extracted from `stanford-parser-3.9.1-models.jar`
            jarRoot = "D:\\MSRA\\Txt2Vis\\Parser\\nlp.stanford.edu\\stanford-corenlp-full-2018-02-27";
            modelsDirectory = jarRoot + "\\edu\\stanford\\nlp\\models";

            // We should change current directory, so StanfordCoreNLP could find all the model files automatically
            var curDir = Environment.CurrentDirectory;
            System.IO.Directory.SetCurrentDirectory(jarRoot);
        }

        //use Stanford.NLP.Net to parse the sentence
        static Tree Parse(string sent)
        {
            // Loading english PCFG parser from file
            var lp = LexicalizedParser.loadModel(modelsDirectory + "\\lexparser\\englishPCFG.ser.gz");

            var tokenizerFactory = PTBTokenizer.factory(new CoreLabelTokenFactory(), "");
            var sentReader = new java.io.StringReader(sent);
            var rawWords = tokenizerFactory.getTokenizer(sentReader).tokenize();
            sentReader.close();
            var tree = lp.apply(rawWords);

            // Extract dependencies from lexical tree
            var tlp = new PennTreebankLanguagePack();
            var gsf = tlp.grammaticalStructureFactory();
            var gs = gsf.newGrammaticalStructure(tree);
            var tdl = gs.typedDependenciesCCprocessed();

            // Extract collapsed dependencies from parsed tree
            //var tp = new TreePrint("penn,typedDependenciesCollapsed");
            var tp = new TreePrint("penn");
            tp.printTree(tree);

            return tree;
        }

        //calculate the two types of description
        static void removePercentage(Tree t, string per, List<string> result)
        {
            string s1 = null, s2 = null;
            while(t.label().value() != "S")
            {
                t = t.firstChild();
            }

            List<Tree> partNP = new List<Tree>();
            List<Tree> partVP_V = new List<Tree>();
            List<Tree> partVP_NP = new List<Tree>();
            string strNP = null;
            string strVP_NP = null;
            string strVP_V = null;
            string strMD = null;
            foreach (Tree child in t.children())
            {
                if (child.label().value() == "NP")
                {
                    partNP.Add(child);
                    strNP += makeString(child) + " ";
                }
                else if (child.label().value() == "VP")
                {
                    Tree validVP;
                    if (child.firstChild().label().value() == "MD")
                    {
                        validVP = child.getChild(1);
                        strMD = makeString(child.firstChild()) + " ";
                    } 
                    else
                        validVP = child;
                    foreach (Tree subChild in validVP.children())
                    {
                        if (isVerb(subChild))
                        {
                            partVP_V.Add(subChild);
                            strVP_V += makeString(subChild) + " ";
                        }
                        else
                        {
                            partVP_NP.Add(subChild);
                            strVP_NP += makeString(subChild) + " ";
                        }
                    }
                }
            }

            if (strNP == null || strVP_NP == null || strVP_V == null)
                return;

            per = per.Replace("%", " %");
            if (strVP_NP.Contains(per))
            {
                //remove percentage
                //strVP_NP = strVP_NP.Replace(per+" ", "");
                strVP_NP = strVP_NP.Substring(strVP_NP.IndexOf(per) + per.Length + 1);//remove percentage and the charaters before it (and a space after it)

                //change the uppercase to lowercase in the old object
                if (strNP[0] >= 'A' && strNP[0] <= 'Z')
                {
                    strNP = char.ToLower(strNP[0]) + strNP.Substring(1);
                }

                strVP_V = toNegative(partVP_V, isContain(partVP_NP, "NNS") || isContain(partVP_NP, "NNPS "), (strMD != null));

                //combine to get the final result
                s1 = strVP_NP + strMD + strVP_V + " " + strNP;
                s2 = strMD + strVP_V + " " + strNP;
            }
            else if(strNP.Contains(per))
            {
                //strNP = strNP.Replace(per+" ", "");
                strNP = strNP.Substring(strNP.IndexOf(per) + per.Length + 1);//remove percentage and the charaters before it (and a space after it)

                //combine to get the final result
                s1 = strNP + strVP_V + strVP_NP;
                s2 = strVP_V + strVP_NP;
            }

            result.Add(s1);
            result.Add(s2);

        }

        static string findTitle(Tree t, int depth, int gran, bool isTarget = true, string avoid = null)
        {
            string s = null;
            List<string> result = new List<string>();
            switch (gran) {
                case 1://return whole scentence
                    avoid = avoid.Replace("%", " % ");
                    s = makeString(t);
                    if (avoid != null)
                        s = s.Replace(avoid, "Percentage ");//s = s.Replace(avoid, string.Empty);
                    s = s.Replace(" .", string.Empty);
                    Console.WriteLine("depth=" + depth + " -- " + s);
                    break;
         
                case 2://return verb-object phrase
                    if (t.label().value() == "VP")
                    {
                        s = makeString(t);
                        Console.WriteLine("depth=" + depth + " -- " + s);
                        return (s);
                    }
                    else
                    {
                        foreach (Tree child in t.children())
                        {
                            s = findTitle(child, ++depth, gran);
                        }
                    }
                    break;

                case 3://return noun phrase
                    //if (isTarget && !isContain(t, "CD"))
                    //    isTarget = false;
                    //if (isTarget)
                    //{
                    //    if (isNoun(t) && !isContain(t, "CD"))
                    //    {
                    //        s = makeString(t);
                    //        Console.WriteLine("depth=" + depth + " -- " + s);
                    //        return (s);
                    //    }

                    //    foreach (Tree child in t.children())
                    //    {
                    //        s = findTitle(child, ++depth, gran, isTarget);
                    //    }
                    //}
                    
                    foreach (Tree child in t.children())
                    {
                        //if (isContain(child, "40 %") && child.label().value() != "S")
                        if (isContain(child, "CD") && child.label().value() != "S")
                            findNoun(child, result);
                        else
                            findTitle(child, ++depth, gran);
                    }
                    break;
                case 4:
                    foreach (Tree child in t.firstChild().children())
                    {
                        if (child.label().value() == "VP")
                            findNoun(child, result);
                    }
                    break;
            }
            return s;
        }

        //make a string according to t
        static string makeString(Tree t)
        {
            if (t != null)
                return string.Join(" ", t.getLeaves().toArray());
            else
                return null;
        }

        //check if tree t contains string s
        static bool isContain(Tree t, string s) {
            if (t.label().value() == s || makeString(t) == s)
                return true;
            foreach (Tree child in t.children())
            {
                if (isContain(child, s))
                    return true;
            }
            return false;
        }

        static bool isContain(List<Tree> t, string s)
        {
            foreach (Tree item in t)
            {
                if (isContain(item, s))
                    return true;
            }
            return false;
        }

        static bool isNoun(Tree t)
        {
            if (makeString(t) == "%")
                return false;
            return t.label().value() == "NP"
                || t.label().value() == "NN"
                || t.label().value() == "NNS"
                || t.label().value() == "NNP"
                || t.label().value() == "NNPS";
            //return t.label().value() == "NP";
        }

        static bool isVerb(Tree t)
        {
            return t.label().value() == "VB"
                || t.label().value() == "VBD"
                || t.label().value() == "VBG"
                || t.label().value() == "VBN"
                || t.label().value() == "VBP"
                || t.label().value() == "VBZ";
        }

        static void findNoun(Tree t, List<string> result)
        {
            if (isNoun(t) && !isContain(t, "CD"))
            {
                result.Add(makeString(t));
                Console.WriteLine(makeString(t));
                return;
            }
            foreach (Tree child in t.children())
            {
                findNoun(child, result);
            }
        }

        static string toNegative(List<Tree> treeList, bool isPlural, bool isFuture = false) {
            if (treeList == null)
                return null;

            string be = null;
            Tree t = treeList[treeList.Count-1];
            if (isFuture)
                be = "be ";
            else
            {
                if (isPlural)
                {
                    switch (t.label().toString())//only tense matters
                    {
                        case "VB"://base form
                        case "VBP"://non-3rd person singular present
                        case "VBZ"://3rd person singular present
                            be = "are ";
                            break;
                        case "VBD"://past tense
                            be = "were ";
                            break;
                        case "VBG"://gerund or present participle
                            be = "are being";
                            break;
                        case "VBN"://past participle
                            be = "were being";
                            break;
                    }
                }
                else//singular or mass
                {
                    switch (t.label().toString())//only tense matters
                    {
                        case "VB"://base form
                        case "VBP"://non-3rd person singular present
                        case "VBZ"://3rd person singular present
                            be = "is ";
                            break;
                        case "VBD"://past tense
                            be = "was ";
                            break;
                        case "VBG"://gerund or present participle
                            be = "is being";
                            break;
                        case "VBN"://past participle
                            be = "was being";
                            break;
                    }
                }
            }
            
            //string verb = makeString(t);
            string verb = new Sentence(makeString(t)).lemma(0);//lemmarize the verb

            if (dict.ContainsKey(verb))
                verb = dict[verb];
            else
                verb = verb + "ed";
            

            return be + verb + " by";
        }
    }


}
