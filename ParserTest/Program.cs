using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;

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
        public string text { get; set; }
        public int length { get; set; }
        /****************************************
         * Description.type:
        0： complete sentence
        1: "whole" for the percentage 
        2: remove percentage
        3: verb-object phrase
        4: remove percentage and all the charaters before percentage (for sentence that can't be parsed proporly)
        5: sentence splitted to three parts -part 1
        6: sentence splitted to three parts -part 2
        7: sentence splitted to three parts -part 3
        *****************************************/
        public int type { get; set; }
    }

    public sealed class DescriptionGen
    {
        //for singleton
        private static DescriptionGen instance = null;
        private static readonly object padlock = new object();

        static Dictionary<string, string> dict = new Dictionary<string, string>();
        static LexicalizedParser lp;
        string jarRoot;
        string modelsDirectory;
        string exeDir;

        public DescriptionGen()
        {

        }

        public DescriptionGen Instance
        {
            get
            {
                lock (padlock)
                {
                    if (instance == null)
                    {
                        instance = new DescriptionGen();
                    }
                    return instance;
                }
            }
        }

        public List<Description> getDescriptions(string sent, string whole, string part, string percentage, string range)
        {
            //set up environment
            //Init();

            List<Description> ldes = new List<Description>();
            //whole sentence
            Description compltSen = new Description();
            compltSen.text = sent;
            compltSen.length = new System.Text.RegularExpressions.Regex(" ").Matches(sent).Count + 1;
            compltSen.type = 0;
            ldes.Add(compltSen);

            //the part of "whole"
            if (whole != null)
            {
                Description wholeSen = new Description();
                wholeSen.text = whole;
                wholeSen.length = new System.Text.RegularExpressions.Regex(" ").Matches(whole).Count + 1;
                wholeSen.type = 1;
                ldes.Add(wholeSen);
            }

            //use Stanford.NLP.Net to parse the sentence
            Tree tree = Parse(sent);

            //calculate the two types of description
            //generate final result

            //List<string> ls = new List<string>();
            ldes = removePercentage(tree, sent, percentage, part, ldes);


            //split sentence to 3 parts
            if (sent.IndexOf(percentage) > 0 && sent.IndexOf(percentage) + percentage.Length + 1 < sent.Length)
            {
                string[] splitSen = new string[3];
                splitSen[0] = sent.Substring(0, sent.IndexOf(percentage) - 1);
                splitSen[1] = percentage;
                splitSen[2] = sent.Substring(sent.IndexOf(percentage) + percentage.Length + 1);
                for (int i = 0; i < splitSen.Length; i++)
                {
                    if (splitSen[i] != null)
                    {
                        Description splitDes = new Description();
                        splitDes.text = splitSen[i];
                        splitDes.length = new System.Text.RegularExpressions.Regex(" ").Matches(splitSen[i]).Count + 1;
                        splitDes.type = i + 5;
                        ldes.Add(splitDes);
                    }
                }
            }

            //the part of "range"
            if (range != null)
            {
                Description rangeSen = new Description();
                rangeSen.text = range;
                rangeSen.length = new System.Text.RegularExpressions.Regex(" ").Matches(range).Count + 1;
                rangeSen.type = 8;
                ldes.Add(rangeSen);
            }

            //the part of "part"
            if (part != null)
            {
                Description partSen = new Description();
                partSen.text = part;
                partSen.length = new System.Text.RegularExpressions.Regex(" ").Matches(part).Count + 1;
                partSen.type = 9;
                ldes.Add(partSen);
            }

            //the part of "percentage + whole"
            if (percentage != null)
            {
                Description percSen = new Description();
                if (percentage.Contains("%") || percentage.Contains("percent") || percentage.Contains("half") || percentage.Contains("Half"))
                    percSen.text = percentage + " of";
               
                percSen.text += " " + whole;
                percSen.length = new System.Text.RegularExpressions.Regex(" ").Matches(percSen.text).Count + 1;
                percSen.type = 10;
                ldes.Add(percSen);
            }


            return ldes;
        }

        //set up environment
        //set up environment
        public void Init()
        {
            // Path to models extracted from `stanford-parser-3.9.1-models.jar`
            jarRoot = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..\\..\\stanford-corenlp-full-2018-02-27");
            modelsDirectory = jarRoot + "\\edu\\stanford\\nlp\\models";

            // We should change current directory, so StanfordCoreNLP could find all the model files automatically
            exeDir = Environment.CurrentDirectory;

            //load json to create dictionary
            try
            {
                string json = System.IO.File.ReadAllText(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\..\\..\\verbs-dictionaries.json");
                JsonSerializer serializer = new JsonSerializer();
                JArray values = JsonConvert.DeserializeObject<JArray>(json);
                foreach (var item in values.Children())
                {
                    dict.Add((string)item[0], (string)item[3]);
                }
            }
            catch (Exception exp)
            {
                Console.WriteLine("Fail loading verb dictionary:\n {0}", exp);
            }

            // Loading english PCFG parser from file
            try
            {
                lp = LexicalizedParser.loadModel(modelsDirectory + "\\lexparser\\englishPCFG.ser.gz");
            }
            catch(Exception exp)
            {
                Console.WriteLine("Fail loading parser model:\n{0}", exp);
            }

            Console.WriteLine("\nParser successfully loaded!\n");
        }


        //use Stanford.NLP.Net to parse the sentence
        Tree Parse(string sent)
        {
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
        List<Description> removePercentage(Tree t, string sent, string per, string part, List<Description> result)
        {
            string s1 = null, s2 = null;

            List<Tree> partNP = new List<Tree>();
            List<Tree> partVP_V = new List<Tree>();
            List<Tree> partVP_NP = new List<Tree>();
            string strNP = null;
            string strVP_NP = null;
            string strVP_V = null;
            string strMD = null;
            string strPreVP = null;
            string strPostVP = null;
            bool appearVP = false;

            string realPer = per.Replace("%", " %");
            if (!makeString(t).Contains(realPer))
                realPer = per;

            if ((!isContain(t, "S") && !isContain(t, "SBAR") ))//to be validate
            {
                Tree subChild = t.firstChild().firstChild();

                if (makeString(t).IndexOf(realPer) != 0 || subChild.label().value() != "NP")//exclude specific pattern like "94% would refer us to a friend"
                {
                    ViolentTrimPerc(sent, per, result);
                    return result;
                }
                //else
                //{
                //    if (subChild.firstChild().label().value() == "NP")
                //    {
                //        partNP.Add(subChild.firstChild());
                //        strNP += makeString(subChild.firstChild()) + " ";
                //    }
                //}
            }


            //go to the deepest "S" structure
            if (t.firstChild().label().value() == "FRAG")
            {
                Tree subChild = t.firstChild().firstChild();

                if (makeString(t.firstChild()).Contains(realPer))
                    t = t.firstChild();

                if (makeString(t).IndexOf(realPer) == 0 && subChild.label().value() == "NP")//for specific pattern like "94% would refer us to a friend"
                {
                    //if (subChild.firstChild().label().value() == "NP")
                    //{
                    //    partNP.Add(subChild.firstChild());
                    //    strNP += makeString(subChild.firstChild()) + " ";
                    //}
                    t = subChild;
                }

                while (makeString(t).Contains(realPer) && (isContain(t, "S") || isContain(t, "SBAR")))
                {
                    bool stop = true;
                    foreach (Tree child in t.children())
                    {
                        //if (makeString(child).Contains(realPer) && isContain(child, "S"))
                        if (isContain(child, "S", realPer) || isContain(child, "SBAR", realPer) )
                        {
                            t = child;
                            stop = false;
                        }
                    }
                    if (stop)
                        break;
                }
            }
            else
            {
                while (makeString(t).Contains(realPer) && (isContain(t, "S") || isContain(t, "SBAR")))
                {
                    bool stop = true;
                    foreach (Tree child in t.children())
                    {
                        //if (makeString(child).Contains(realPer) && isContain(child, "S"))
                        if (isContain(child, "S", realPer) || isContain(child, "SBAR", realPer) || isContain(child, "SINV", realPer))
                        {
                            t = child;
                            stop = false;
                        }
                    }
                    if (stop)
                        break;
                }
            }

            //distribute different parts to corresponding variables
            bool hasBe=false;
            foreach (Tree child in t.children())
            {
                if ((!appearVP && child.label().value() == "NP") || (appearVP && child.label().value() == "PP") || (child.label().value() == "ADJP"))
                {
                    strNP += makeString(child) + " ";
                    partNP.Add(child);
                }
                else if (child.label().value() == "VP" || child.label().value() == "SBAR" || (appearVP && child.label().value() != "." && child.label().value() != ":"))
                {
                    appearVP = true;
                    Tree validVP = GetValidVP(child, partVP_V, ref strVP_V, ref strMD, ref strPreVP, ref hasBe, realPer);
                    if (strPreVP!=null && (strPreVP.Contains("have") || strPreVP.Contains("has") || strPreVP.Contains("had")))//for perfect tense
                        strVP_V = strPreVP + strVP_V;
                    else
                        strVP_V += strPreVP;

                    //hasBe = false;
              
                    int i = 0;
                    bool appearVerb = false;
                        
                    foreach (Tree subChild in validVP.children())
                    {

                        //if (isBe(subChild))
                        //{
                        //    hasBe = true;
                        //    partVP_V.Add(subChild);
                        //    strVP_V += makeString(subChild) + " ";
                        //    break;
                        //}
                        if (isVerb(subChild))
                        {
                            partVP_V.Add(subChild);
                            strVP_V += makeString(subChild) + " ";
                            appearVerb = true;
                        }else if (appearVerb && subChild.label().value() == "PRT")
                        {
                            strPostVP += makeString(subChild) + " ";
                        }else if (!appearVerb && subChild.label().value() == "ADVP")
                        {
                            strPreVP += makeString(subChild) + " ";
                        }
                        else
                        {
                            strVP_NP += makeString(subChild) + " ";
                            if(partVP_V.Count>0 && subChild.label().value() != "ADVP")
                                partVP_NP.Add(subChild);
                        }
                           
                        i++;
                    }
              
                }
            }

            //if (strNP == null && strVP_NP == null && strVP_V == null)
            //{
            //    ViolentTrimPerc(sent, per, result);
            //    return result;
            //}

            //combine different part to get final result
            if (strVP_NP!=null && strVP_NP.Contains(realPer))//percentage in object
            {
                string trimStrVP_NP = strVP_NP;
                if (strVP_NP != null)
                {
                    //remove percentage
                    //strVP_NP = strVP_NP.Replace(realPer+" ", "");
                    if(strVP_NP.Contains(realPer))
                        strVP_NP = strVP_NP.Substring(strVP_NP.IndexOf(realPer) + realPer.Length + 1);//remove percentage and the charaters before it (and a space after it)
                    if (strVP_NP != "")
                    {
                        trimStrVP_NP = strVP_NP[0] == ' ' ? strVP_NP.Substring(1) : strVP_NP;
                        trimStrVP_NP = trimStrVP_NP[trimStrVP_NP.Length - 1] == ' ' ? trimStrVP_NP.Substring(0, trimStrVP_NP.Length - 1) : trimStrVP_NP;
                    }
                }

                if(strNP != null) { 
                    //change the uppercase to lowercase in the old object
                    if (strNP[0] >= 'A' && strNP[0] <= 'Z')
                    {
                        strNP = char.ToLower(strNP[0]) + strNP.Substring(1);
                    }
                }

                if(strVP_V != null)
                    strVP_V = toNegative(partVP_V, isContain(partVP_NP, "NNS") || isContain(partVP_NP, "NNPS "), strPreVP, strPostVP, (strMD != null));

                //combine to get the final result
                if (strNP != null && strNP != " " && sent.Contains(trimStrVP_NP) && ((!hasBe && partVP_V.Count < 2)||(hasBe && partVP_V.Count < 3)))
                {
                    s1 = strVP_NP + strMD + strVP_V + " " + strNP;
                    //s1 = strVP_NP;
                    s2 = strMD + strVP_V + " " + strNP;
                }
                else
                {
                    s1 = strVP_NP;
                    s2 = null;
                }
            }
            //else if(strNP != null && strNP.Contains(per))//percentage in subject
            else//percentage in subject
            {
                //strNP = strNP.Replace(realPer+" ", "");
                if (strNP != null)
                {
                    if (strNP.Contains(realPer))
                        strNP = strNP.Substring(strNP.IndexOf(realPer) + realPer.Length + 1);//remove percentage and the charaters before it (and a space after it)
                }

                //combine to get the final result
                if (strPreVP !=null && !strVP_V.Contains(strPreVP))
                    strVP_V = strPreVP + strVP_V;
                s1 = strNP + strMD + strVP_V + strVP_NP;
                s2 = strMD + strVP_V + strVP_NP;
            }

            if (s1 != "" && s1 != null && s1 != " ")
            {
                //result.Add(CorrectFormat(s1));
                Description sen = new Description();
                sen.text = CorrectFormat(s1);
                sen.length = new System.Text.RegularExpressions.Regex(" ").Matches(s1).Count;
                sen.type = 2;
                result.Add(sen);
            }
            if (s2 != "" && s2 != null && s2 != " ")
            {
                //result.Add(CorrectFormat(s2));
                Description sen = new Description();
                sen.text = CorrectFormat(s2);
                sen.length = new System.Text.RegularExpressions.Regex(" ").Matches(s2).Count;
                sen.type = 3;
                result.Add(sen);
            }

            if ((s1 == null || s1 == "") && (s2 == null || s2 == ""))//to be valid
                ViolentTrimPerc(sent, per, result);

            return result;
        }

        void ViolentTrimPerc(string sent, string per, List<Description> result)
        {
            string s1 = sent.Replace(per, "");
            string s2 = sent.Substring(sent.IndexOf(per) + per.Length);//remove percentage and the charaters before it (and a space after it));

            //remove punctuation in the end
            s2 = CorrectFormat(s2);
            int i1 = 0, i2 = s2.Length - 1;
            for (i1 = 0; i1 < s2.Length; i1++)
            {
                if (s2[i1] != ' ' && s2[i1] != '-' && s2[i1] != ':' && s2[i1] != '=' && s2[i1] != '–' && s2[i1] != '.' && s2[i1] != ';' && s2[i1] != '~')
                    break;
            }
            for (i2 = s2.Length - 1; i2 >= 0; i2--)
            {
                if (s2[i2] != ' ' && s2[i2] != '-' && s2[i2] != ':' && s2[i2] != '=' && s2[i2] != '–' && s2[i2] != '.' && s2[i2] != ';' && s2[i2] != '~')
                    break;
            }

            if (i2 > i1)
            {
                //result.Add(CorrectFormat(s2));
                Description sen = new Description();

                sen.text = s2.Substring(i1, i2 - i1 + 1);
                sen.length = new System.Text.RegularExpressions.Regex(" ").Matches(sen.text).Count;
                sen.type = 4;
                result.Add(sen);
                return;
            }


            //remove punctuation in the end
            s1 = CorrectFormat(s1);
            i1 = 0;
            i2 = s1.Length - 1;
            for (i1 = 0; i1 < s1.Length; i1++)
            {
                if (s1[i1] != ' ' && s1[i1] != '-' && s1[i1] != ':' && s1[i1] != '=' && s1[i1] != '–' && s1[i1] != '.' && s1[i1] != ';' && s1[i1] != '~')
                    break;
            }
            for (i2 = s1.Length - 1; i2 >= 0; i2--)
            {
                if (s1[i2] != ' ' && s1[i2] != '-' && s1[i2] != ':' && s1[i2] != '=' && s1[i2] != '–' && s1[i2] != '.' && s1[i2] != ';' && s1[i2] != '~')
                    break;
            }

            if (i2 > i1)
            {
                //result.Add(CorrectFormat(s1));
                Description sen = new Description();

                sen.text = s1.Substring(i1, i2 - i1 + 1);
                sen.length = new System.Text.RegularExpressions.Regex(" ").Matches(sen.text).Count;
                sen.type = 4;
                result.Add(sen);
            }
        }

        Tree GetValidVP(Tree originVP, List<Tree> partVP_V, ref string strVP_V, ref string strMD, ref string strPreVP, ref bool hasBe, string per)
        {
            Tree validVP = originVP;
            //if (originVP.firstChild().label().value() == "MD")
            //{
            //    strMD += makeString(originVP.firstChild()) + " ";
            //    validVP = originVP.getChild(1);
            //}
            //else
            //    validVP = originVP;

            bool flag = false;
            foreach(Tree child in validVP.children())
            {
                // if (child.label().value() == "VP" && makeString(child).Contains(per))
                if (child.label().value() == "VP" )
                {
                    flag = true;
                    break;
                }

            }
            if (!flag)
                return validVP;

            int i = 0;
            foreach (Tree subChild in validVP.children())
            {
                if (subChild.label().value() == "CC")
                    break;
                //else if (subChild.label().value() == "VP" && makeString(subChild).Contains(per))
                else if (subChild.label().value() == "VP")
                {
                    validVP = GetValidVP(subChild, partVP_V, ref strVP_V, ref strMD, ref strPreVP, ref hasBe, per);
                }
                else if (isBe(subChild))
                {
                    validVP.removeChild(i);
                    partVP_V.Add(subChild);
                    strVP_V += makeString(subChild) + " ";
                    hasBe = true;
                }
                else if(subChild.label().value() == "MD")
                {
                    strMD += makeString(subChild) + " ";
                }
                //else if (subChild.label().value() == "ADVP"
                //      || makeString(subChild).Contains("have")
                //      || makeString(subChild).Contains("has")
                //      || makeString(subChild).Contains("had")
                //      || makeString(subChild).Contains("do")
                //      || makeString(subChild).Contains("does")
                //      || makeString(subChild).Contains("did")
                //      || makeString(subChild).Contains("not"))
                else
                {
                    strPreVP += makeString(subChild) + " ";
                }
                i++;
            }

            return validVP;
        }

        Tree LocateString (Tree t, string s)
        {
            
            return t;
        }

        //make a string according to t
        string makeString(Tree t)
        {
            if (t != null)
                return string.Join(" ", t.getLeaves().toArray());
            else
                return null;
        }

        //check if tree t contains string s
        bool isContain(Tree t, string s) {
            if (t.label().value() == s)
                return true;
            foreach (Tree child in t.children())
            {
                if (isContain(child, s))
                    return true;
            }
            return false;
        }

        bool isContain(Tree t, string s, string per)
        {
            if (t.label().value() == s && makeString(t).Contains(per))
                return true;
            foreach (Tree child in t.children())
            {
                if (isContain(child, s, per))
                    return true;
            }
            return false;
        }

        bool isContain(List<Tree> t, string s)
        {
            foreach (Tree item in t)
            {
                if (isContain(item, s))
                    return true;
            }
            return false;
        }

        bool isNoun(Tree t)
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

        bool isVerb(Tree t)
        {
            return t.label().value() == "VB"
                || t.label().value() == "VBD"
                || t.label().value() == "VBG"
                || t.label().value() == "VBN"
                || t.label().value() == "VBP"
                || t.label().value() == "VBZ";
        }

        bool isBe(Tree t)
        {
            return makeString(t).Contains("am")
                || makeString(t).Contains("is")
                || makeString(t).Contains("are")
                || makeString(t).Contains("was")
                || makeString(t).Contains("were")
                || makeString(t).Contains("be")
                || makeString(t).Contains("been");
        }

        bool isBe(string s)
        {
            return s.Contains("am")
                || s.Contains("is")
                || s.Contains("are")
                || s.Contains("was")
                || s.Contains("were")
                || s.Contains("be")
                || s.Contains("been");
        }

        string toNegative(List<Tree> treeList, bool isPlural, string strPreVerb, string strPostVerb, bool isFuture = false) {
            if (treeList == null || treeList.Count == 0)
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
                            be = "are being ";
                            break;
                        case "VBN"://past participle
                            be = null;
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
                            be = "is being ";
                            break;
                        case "VBN"://past participle
                            be = null;
                            break;
                    }
                }
            }

            try
            {
                System.IO.Directory.SetCurrentDirectory(jarRoot);
            }
            catch (Exception exp)
            {
                Console.WriteLine("Fail changing directory: {0}", exp);
            }

            string verb = null;
            try
            {
                verb = new Sentence(makeString(t)).lemma(0);//lemmarize the verb
            }
            catch (Exception e)
            {
                Console.WriteLine("Fail lemmarizing the verb: {0}", e);
            }

            if (isBe(verb))
                verb = null;
            else if (dict.ContainsKey(verb))
                verb = dict[verb];
            else
                verb = verb + "ed";

            if(be != null && verb != null)
            {
                if (verb != null)
                    verb = verb + " " + strPostVerb + " by";
            }

            return be + strPreVerb + verb;
        }

        string CorrectFormat(string s)
        {
            string news = s;
            //remove unnecessary space
            if (s != null)
            {
                s = s.Replace("-LRB- ", "(");
                s = s.Replace(" -RRB-", ")");
                s = s.Replace("-LSB- ", "[");
                s = s.Replace(" -RSB-", "]");
                s = s.Replace("  ", " ");
                s = s.Replace("( ", "(");
                s = s.Replace(" )", ")");
                s = s.Replace(" ''", "\"");
                s = s.Replace("`` ", "\"");
                s = s.Replace("--", "-");
                s = s.Replace(" ,", ",");
                s = s.Replace(" .", ".");
                s = s.Replace(" *", "*");
                s = s.Replace(" '", "'");
                s = s.Replace(" +", "+");
                s = s.Replace(" - ", "-");
                s = s.Replace("$ ", "$");
                s = s.Replace(" ;", ";");
                s = s.Replace(" n't", "n't");
            }

            return s;
        }
    }

    public class Program
    {
        static void Main(string[] args)
        {

            // This option shows loading and using an explicit tokenizer
            //var sent = "70% of the World's Population now own a mobile phone."; var percentage = "70%";
            //var sent = "70% of hiring managers said it's more important than IQ"; var percentage = "16%";
            //var sent = "less than 1% of US men knew how to tie a bow tie."; var percentage = "1%";
            //var sent = "8 in 10 small business owners applied for some form of financing in 2015"; var percentage = "8 in 10";

            //var sent = "62% are familiar with alternative business loans."; var percentage = "16%";
            //var sent = "300 thousand seabirds are killed each year."; var percentage = "16%";
            //var sent = "40% from defense, 60% from nondefense programs."; var percentage = "16%";
            //var sent = "12% of e-mail users have actually tried to buy stuff from spam"; var percentage = "16%";
            //var sent = "1/3rd of recent college grads have delayed purchasing a house or a car because of their debt."; var percentage = "1/3rd";
            //var sent = "Less than 2% of our planets ocean is protected from factors that kill marine life and destroy environments."; var percentage = "16%";
            //var sent = "45% -- the employment rate of dropouts"; var percentage = "16%";
            //var sent = "Young children who watch sesame street had 16% higher GPAs in high school."; var percentage = "16%";
            //var sent = "Last month, 206 D.C. public school teachers were fired for poor performance under IMPACT, amounting to 5 percent of the 4,100 teachers in the city school system."; var percentage = "5 percent";
            //var sent = "People will read about 20% of the text on the average web page."; var percentage = "20%";

            DescriptionGen generator = new DescriptionGen();
            generator.Init();

            //for debug
            //var sent = "57% of the world’s fish is already exploited";
            //var percentage = "57%";
            //var whole = "boys";
            //var part = "like video games";
            //var range = "";

            //List<Description> ldes = generator.getDescriptions(sent, whole, part, percentage, range);
            //Console.WriteLine("{0}\n", sent);
            //foreach (Description des in ldes)
            //{
            //    Console.WriteLine(des.type + " - " + des.text);
            //}

            ////}
            //Console.WriteLine("\n");
            //Console.ReadLine();

            //for json
            string sent = null;
            string percentage = null, part = null, whole = null, range = null;
            string json = System.IO.File.ReadAllText("..\\..\\PercentText_label_2_test62.json");
            //string json = System.IO.File.ReadAllText("..\\..\\PercentText_label_2_train538.json");
            JsonSerializer serializer = new JsonSerializer();
            var text = JsonConvert.DeserializeObject<List<Data>>(json);
            int errCount1 = 0;
            int errCount0 = 0;
            int errFail = 0;
            int textCount = 0;
            //foreach (var item in text)
            //{
            for (int i = 52; i < 53; i++)
            {
                var item = text[i];
                percentage = null; part = null; whole = null; range = null;
                //follow up process
                Console.WriteLine("Sentence " + textCount);

                sent = item.text;
                foreach (var entity in item.annotation.entities)
                {
                    if (entity.label == "Whole")
                    {
                        whole = item.text.Substring(Int32.Parse(entity.start), Int32.Parse(entity.length));
                        Console.WriteLine("Whole:  " + whole);
                    }
                    else if (entity.label == "Part")
                    {
                        part = item.text.Substring(Int32.Parse(entity.start), Int32.Parse(entity.length));
                        Console.WriteLine("Part:   " + part);
                    }
                    else if (entity.label == "Range")
                    {
                        range = item.text.Substring(Int32.Parse(entity.start), Int32.Parse(entity.length));
                        Console.Write("Range:  " + range + "          ");
                    }
                }
                foreach (var feature in item.annotation.features)
                {
                    if (feature.label == "Percentage")
                    {
                        percentage = item.text.Substring(Int32.Parse(feature.start), Int32.Parse(feature.length));
                        Console.WriteLine("Prctg:  " + percentage);
                    }
                }

                Console.WriteLine("Sent:   {0}\n", sent);


                List<Description> ldes = generator.getDescriptions(sent, whole, part, percentage, range);


                //print result
                foreach (Description des in ldes)
                {
                    if (des.type == 0 || des.type == 2 || des.type == 3 || des.type == 4)
                    //if (des.type == 10)
                        Console.WriteLine(des.type + " - " + des.text);
                }
                Console.WriteLine("\n");
                //Console.ReadLine(); 

                if (ldes.Count == 6)
                {
                    errCount1++;
                    //Console.ReadLine();
                }
                else if (ldes.Count == 5)
                {
                    errCount0++;
                    //Console.ReadLine();
                }
                textCount++;
            }
            Console.WriteLine("err1:" + errCount1 + " err0:" + errCount0 + " fail:" + errFail + "\n");



            Console.ReadLine();
        }
        
    }
}
