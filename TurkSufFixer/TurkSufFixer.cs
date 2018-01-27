using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TurkSufFixer
{
    public class SufFixer
    {
        public  static readonly List<string> suffixes = new List<string>()
        {
          Suffixes.ACC,
          Suffixes.DAT,
          Suffixes.LOC,
          Suffixes.ABL,
          Suffixes.INS,
          Suffixes.PLU,
          Suffixes.GEN
        };
        private static readonly string vowels = "aıuoeiüö";
        //private static readonly string backvowels = "aıuo";
        private static readonly string frontvowels = "eiüö";
        private static readonly string backunrounded = "aı";
        private static readonly string backrounded = "uo";
        private static readonly string frontunrounded = "ei";
        private static readonly string frontrounded = "üöûô";
        private static readonly string roundedvowels = "uoüö";
        private static readonly string hardconsonant = "fstkçşhp";
        private static readonly string H = "ıiuü";
        private static readonly string[] numbers = {"sıfır","bir","iki","üç","dört","beş","altı","yedi","sekiz","dokuz" };
        private static readonly string[] tens = {"sıfır", "on", "yirmi", "otuz", "kırk", "elli", "altmış", "yetmiş", "seksen", "doksan" };
        private string possesiveFilePath;
        private Regex time_pattern = new Regex("([01]?[0-9]|2[0-3])[.:]00");
        private static Dictionary<int, string> digits = new Dictionary<int, string>()
        {
            {0, "yüz"},
            {3, "bin"},
            {6, "milyon"},
            {9, "milyar"},
            {12, "trilyon"},
            {15, "katrilyon"}
        };
        private static TupleList<char,char> consonantTuple = new TupleList<char,char>()
        {
            {'ğ', 'k'},
            {'g', 'k'},
            {'d', 't'},
            {'c', 'ç'},
            {'b', 'p'}
        };
        private static TupleList<string, char> translate_table = new TupleList<string, char>()
        {
            {"ae",'A'},
            {"ıuüi",'H'}
        };
        private static TupleList<char, char> accent_table = new TupleList<char, char>()
        {
            {'â', 'e'},
            {'î', 'i'},
            {'û', 'ü'},
            {'ô', 'ö'}

        };
        List<string> possessivesuff = new List<string>(4)
        {
            "lArH", "H", "yH", "sH", "lHğH"
        };
        private Dictionary<string, string> superscript = new Dictionary<string, string>()
        {
            {"²", "kare"},
            {"³", "küp"}
        };
        private Dictionary<string, string> others = new Dictionary<string,string>();
        private HashSet<string> dictionary;
        private HashSet<string> possesive;
        private HashSet<string> exceptions;
        private HashSet<string> haplology;

        private bool updated = false;
        System.Globalization.CultureInfo turkishCulture =  new System.Globalization.CultureInfo("tr-TR");
        public SufFixer(string dictpath = @"sozluk/kelimeler.txt", string exceptpath = @"sozluk/istisnalar.txt",
                      string haplopath = @"sozluk/unludusmesi.txt", string poss = @"sozluk/iyelik.txt", string othpath = @"sozluk/digerleri.txt")
        {
            try
            {
                possesiveFilePath = poss;
                dictionary = new HashSet<string>(File.ReadAllLines(dictpath, Encoding.UTF8));
                possesive = new HashSet<string>(File.ReadAllLines(poss, Encoding.UTF8));
                exceptions = new HashSet<string>(File.ReadAllLines(exceptpath, Encoding.UTF8));
                haplology = new HashSet<string>(File.ReadAllLines(haplopath, Encoding.UTF8));
                dictionary.UnionWith(exceptions);
                dictionary.UnionWith(haplology);
                Regex a = new Regex("(\\w+) +-> +(\\w+)", RegexOptions.CultureInvariant);
                foreach (var fline in File.ReadAllLines(othpath, Encoding.UTF8))
                {
                    string line = turkishSanitize(fline);
                    var match = a.Match(line);
                    if (match.Success)
                    {
                        others.Add(match.Groups[1].Value, match.Groups[2].Value);
                    }
                    else
                    {
                        others.Add(line, line + (line.EndsWith("k") ? "a" : "e"));
                    }
                }
            }
            catch (IOException e)
            {
                throw new DictionaryNotFoundException(e.Message,e);
            }
        }

        private string readNumber(string number)
        {
            var time_match = time_pattern.Match(number);
            if (time_match.Success)
            {
                number = time_match.Groups[1].Value;
            }

            for (int i = number.Length - 1; i >= 0; i--)
            {
                if (!number[i].Equals('0') && Regex.IsMatch(number[i].ToString(), @"\d"))
                {
                    int n = (int)Char.GetNumericValue(number[i]);
                    i = number.Length - i - 1;
                    if (i == 0)
                        return numbers[n];
                    else if (i == 1)
                        return tens[n];
                    else
                    {
                        n = (i / 3) * 3;
                        n = n < 15 ? n : 15;
                        return digits[n];
                    }
                }

            }

            return "sıfır";
        }
        private IEnumerable<Tuple<string,string>> divideWord(string name, string suffix = "")
        {
            string realsuffix = name.Substring(name.Length - suffix.Length);
            name = suffix.Length > 0 ? name.Substring(0, name.Length - suffix.Length) : name;
            TupleList<string, string> result = new TupleList<string, string>();
            if (dictionary.Contains(name) || checkConsonantHarmony(name, suffix))
                yield return new Tuple<string, string>("", name);
            else
            {
                string realname = checkEllipsisAffix(name, realsuffix);
                if (realname != "") yield return new Tuple<string, string>("", realname);
            }
            for (int i = 2; i < name.Length -1; i++)
            {
                string firstWord  = name.Substring(0, i);
                string secondWord = name.Substring(i);
                if (dictionary.Contains(firstWord))
                {
                    if (dictionary.Contains(secondWord) || checkConsonantHarmony(secondWord, suffix))
                        yield return new Tuple<string, string>(firstWord, secondWord);
                    else
                    {
                        secondWord = checkEllipsisAffix(secondWord, realsuffix);
                        if (secondWord != "") yield return new Tuple<string, string>(firstWord, secondWord);
                    }
                }

            }
        }
        private string checkEllipsisAffix(string name, string realsuffix)
        {
            if (!H.Contains(realsuffix)) return "";
            name = name.Substring(0, name.Length - 1) + realsuffix + name.Last();
            return haplology.Contains(name) ? name : "";
        }
        private bool checkConsonantHarmony(string name, string suffix)
        {
            string substr = name.Substring(0, name.Length - 1);
            return (suffix.Equals("H") && consonantTuple.Any(t =>
                name.Last().Equals(t.Item1) && dictionary.Contains(substr + t.Item2)));
                /*if last letter is in gğbcd*/ /* Replace it and check it in dictionary */
        }
        private bool checkVowelHarmony(string name, string suffix)
        {
            char lastVowelOfName = ' ', firstVowelOfSuffix = ' ';
            bool isFrontVowel = false;
            if (exceptions.Contains(name))
                isFrontVowel = true;

            lastVowelOfName = name.Last(c => vowels.Contains(c));
            firstVowelOfSuffix = suffix.First(c => vowels.Contains(c));

            bool frontness = (frontvowels.Contains(lastVowelOfName) || isFrontVowel) == (frontvowels.Contains(firstVowelOfSuffix));
            bool roundness = (roundedvowels.Contains(lastVowelOfName) == roundedvowels.Contains(firstVowelOfSuffix));
            return frontness && (roundness || !H.Contains(firstVowelOfSuffix));
        }
        private string surfacetolex(string suffix)
        {
            foreach (var each in translate_table)
            {
                foreach (var letter in each.Item1)
                {
                    suffix = suffix.Replace(letter, each.Item2);
                }
            }
            return suffix;
        }
        private bool checkCompoundNoun(string name)
        {
            if (name.EndsWith("oğlu"))
                return true;
            var probablesuff = new Dictionary<string, string>(4);
            for (int i = 1; i < 5 && i < name.Length; i++)
			{
                string temp = name.Substring(name.Length - i);
                probablesuff.Add(surfacetolex(temp), temp);
			}

            foreach (var posssuff in possessivesuff)
            {
                string realsuffix;
                if (probablesuff.TryGetValue(posssuff, out realsuffix))
                {
                    var wordpairs = divideWord(name, posssuff);
                    foreach (var wordpair in wordpairs)
                    {
                        if (checkVowelHarmony(wordpair.Item2,realsuffix)){
                            updated = true;
                            possesive.Add(name);
                            return true;
                        }

                    }
                }
            }
            return false;
        }
        private bool checkExceptionalWord(string name)
        {
            return divideWord(name).Any(word => !word.Item1.Equals("") && exceptions.Contains(word.Item2));
        }
        public string makeAccusative(string name, bool apostrophe = true)
        {
            return constructName(name, Suffixes.ACC, apostrophe);
        }
        public string makeDative(string name, bool apostrophe = true)
        {
            return constructName(name, Suffixes.DAT, apostrophe);
        }
        public string makeLocative(string name, bool apostrophe = true)
        {
            return constructName(name, Suffixes.LOC, apostrophe);
        }
        public string makeAblative(string name, bool apostrophe = true)
        {
            return constructName(name, Suffixes.ABL, apostrophe);
        }
        public string makeInstrumental(string name, bool apostrophe = true)
        {
            return constructName(name, Suffixes.INS, apostrophe);
        }
        public string makePlural(string name, bool apostrophe = true)
        {
            return constructName(name, Suffixes.PLU, apostrophe);
        }
        public string makeGenitive(string name, bool apostrophe = true)
        {
            return constructName(name, Suffixes.GEN, apostrophe);
        }
        private string constructName(string name, string suffix, bool apostrophe = true)
        {
            return string.Format("{0}{1}{2}", name, apostrophe ? "'" : "", getSuffix(name, suffix));
        }
        public string getSuffix(string name, string suffix)
        {
            name = turkishSanitize(name);

            if (name.Length == 0)
            {
                throw new EmptyNameException("Given name should contains at least 1 character!");
            }
            if (suffixes.All<string>(s => !s.Equals(suffix)))
            {
                throw new NotValidSuffixException();
            }
            string rawsuffix = suffix;
            bool soft = false;
            var split = name.Split(' ');
            name = split.Last();
            if ((H.Contains(name.Last()) && (!rawsuffix.Equals(Suffixes.PLU) && !rawsuffix.Equals(Suffixes.INS)) && 
                (split.Length > 1 || !dictionary.Contains(name)) && (possesive.Contains(name) || checkCompoundNoun(name))))
            {
                suffix = 'n' + suffix;
            }
            else if (Regex.IsMatch(name.Last().ToString(), @"\d"))
            {
                name = readNumber(name);
            }
            else if(exceptions.Contains(name) || (!dictionary.Contains(name) && checkExceptionalWord(name))){
                soft = true;
            }
            else if (others.ContainsKey(name))
            {
                name = others[name];
            }
            else if (superscript.ContainsKey(name.Last().ToString()))
            {
                name = superscript[name.Last().ToString()];

            }
            char lastVowel = name.LastOrDefault(c => vowels.Contains(c));
            if (lastVowel.Equals('\0')){
                lastVowel = name.EndsWith("k") ? 'a' : 'e';
                name = name + (name.EndsWith("k") ? 'a' : 'e');
            }

            if (suffix.Last() == 'H')
                suffix = suffix.Replace('H',findReplacement(lastVowel,soft));

            else {
                if (frontvowels.Contains(lastVowel) || soft)
                    suffix =  suffix.Replace('A','e');
                else
                    suffix = suffix.Replace('A', 'a');

                if (hardconsonant.Contains(name.Last()))
                    suffix = suffix.Replace('D','t');
                else
                    suffix = suffix.Replace('D','d');
            }

            if (vowels.Contains(name.Last())){
               if (vowels.Contains(suffix[0]) || (rawsuffix.Equals(Suffixes.INS)))
                suffix = (rawsuffix.Equals(Suffixes.GEN) ? 'n' : 'y') + suffix;
            }
            return suffix;
        }
        private char findReplacement(char lastVowel, bool soft)
        {
            if (frontrounded.Contains(lastVowel) || (soft && backrounded.Contains(lastVowel)))
	        {
		        return 'ü';
	        }
            else if (frontunrounded.Contains(lastVowel) || (soft && backunrounded.Contains(lastVowel)))
	        {
		        return 'i';
	        }
            else if (backrounded.Contains(lastVowel))
	        {
		        return 'u';
	        }
		    return 'ı';
        }
        private string turkishSanitize(string name)
        {
            name = name.Trim();
            name = name.ToLower(turkishCulture);
            foreach (var rep_char in accent_table)
            {
                name = name.Replace(rep_char.Item1, rep_char.Item2);
            }
            return name;
        }
        ~SufFixer()
        {
            try {
                if (updated) File.WriteAllLines(possesiveFilePath, possesive, Encoding.UTF8);
            }
            catch(IOException e)
            {
                throw new DictionaryException("Dictionary updating problem", e);
            }
        }

    }
    [Serializable]
    public class SuffixException : Exception
    {
        public SuffixException()
        { }
        public SuffixException(string message) : base(message)
        { }
        public SuffixException(string message, Exception inner) : base(message,inner)
        { }
    }
    [Serializable]
    public class EmptyNameException : SuffixException
    {
        public EmptyNameException(string message) : base(message)
        { }
    }
    [Serializable]
    public class NotValidSuffixException : SuffixException
    {
        public NotValidSuffixException()
        { }
    }
    [Serializable]
    public class DictionaryException : SuffixException
    {
        public DictionaryException(string message, Exception e) : base(message, e)
        { }
    }
    [Serializable]
    public class DictionaryNotFoundException : SuffixException
    {
        public DictionaryNotFoundException(string message, Exception e) : base(message, e)
        { }
    }
    
    class TupleList<T1, T2> : List<Tuple<T1, T2>>, IEnumerable<Tuple<T1,T2>>
    {
        public void Add(T1 item, T2 item2)
        {
            Add(new Tuple<T1, T2>(item, item2));
        }
    }
    public static class Suffixes
    {
        public static String ACC { get { return "H"; } }
        public static String DAT { get { return "A"; } }
        public static String LOC { get { return "DA"; } }
        public static String ABL { get { return "DAn"; } }
        public static String INS { get { return "lA"; } }
        public static String PLU { get { return "lAr"; } }
        public static String GEN { get { return "Hn"; } }
    }
}
