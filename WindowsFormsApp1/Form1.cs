 using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            BNF_text.Text = File.ReadAllText("TextFile1.txt");
        }

        //Синхронизация скроллинга двух richtextbox
        public enum ScrollBarType : uint
        {
            SbHorz = 0, SbVert = 1, SbCtl = 2, SbBoth = 3
        }
        public enum Message : uint
        { WM_VSCROLL = 0x0115 }
        public enum ScrollBarCommands : uint
        { SB_THUMBPOSITION = 4 }
        [DllImport("User32.dll")]
        public extern static int GetScrollPos(IntPtr hWnd, int nBar);
        [DllImport("User32.dll")]
        public extern static int SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        //Конец блока для синхронизации

        private void text_In_VScroll(object sender, EventArgs e)
        {
            int nPos = GetScrollPos(text_In.Handle, (int)ScrollBarType.SbVert);
            nPos <<= 16; uint wParam = (uint)ScrollBarCommands.SB_THUMBPOSITION | (uint)nPos;
            SendMessage(text_numeric.Handle, (int)Message.WM_VSCROLL, new IntPtr(wParam), new IntPtr(0));
        }

        private void text_In_ContentsResized(object sender, ContentsResizedEventArgs e)
        {
            int linesCount = text_In.Lines.Count();
            text_numeric.Text = string.Empty;
            if (linesCount == 0) return;
            string text = string.Empty;
            for (int i = 1; i < linesCount; i++)
            { text = text + i.ToString() + Environment.NewLine; }
            text = text + linesCount.ToString();
            text_numeric.Text = text;
            text_In_VScroll(sender, e);
        }


        ///////////////////////////////////////////////
        public List<string> reservedWords = new List<string> { "Begin", "End", "sin", "cos", "tg", "ctg" };
        List<List<string>> wordsTable;
        Dictionary<string, string> variables;
        bool clearSelection = false;
        int currentRow = 0;
        int currentWord = 0;

        bool yrav = false;
        bool poly = false;

        bool semicolon = true;
        public enum VariableErrorType : int
        {
            Correct = 0,
            FirstDigit = 1,
            FirstUnknownChar = 2,
            UnknownChar = 3,
            ReserverWord = 4
        }

        public enum NumericErrorType : int
        {
            Correct = 0,
            NotNumeric = 1,
            Overflow = 2,
            DoubleNumer = 3
        }

        private void button1_Click(object sender, EventArgs e)
        {
            int linesCount = text_In.Lines.Count();
            text_Out.Text = string.Empty;
            text_Out.ForeColor = Color.Black;
            if (linesCount == 0)
            {
                MessageBox.Show("Ошибка! На вход подан пустой текст!"); return;
            }
            else
            { CollectAndOrganizeData(); }
        }

        private void CollectAndOrganizeData()
        {
            if (wordsTable != null) wordsTable.Clear();
            wordsTable = new List<List<string>>();
            clearSelection = false;
            currentRow = 0;
            currentWord = 0;
            yrav = poly = false;
            semicolon = true;
            if (variables != null) variables.Clear();
            variables = new Dictionary<string, string>();
            foreach (var line in text_In.Lines)
            {
                string code = line.Replace(",", " , ").Replace("=", " = ").Replace("\r\n", " ").Replace("*", " * ").Replace("/", " / ").Replace("-", " - ").Replace("+", " + ").Replace("sin", " sin ").Replace("cos", " cos ").Replace("tg", " tg ").Replace("ctg", " ctg ").Replace("^", " ^ ");
                RegexOptions options = RegexOptions.None;
                Regex regex = new Regex("[ ]{2,}", options);
                code = regex.Replace(code, " ");
                code = code.Trim();
                wordsTable.Add(code.Split(' ').ToList());
            }
            AnalizeLanguage();
        }

        private bool CheckFirstWord(string wordToFind)
        {
            foreach (List<string> row in wordsTable.Skip(currentRow))
            {
                foreach (string word in row.Skip(currentWord))
                {
                    currentWord++;
                    if (word != string.Empty)
                    {
                        return word.Equals(wordToFind);
                    }
                }
                currentRow++;
                currentWord = 0;
            }
            return false;
        }

        void changeLine(RichTextBox RTB, int line, string text)
        {
            int s1 = RTB.GetFirstCharIndexFromLine(line);
            int s2 = line < RTB.Lines.Count() - 1 ? RTB.GetFirstCharIndexFromLine(line + 1) - 1 : RTB.Text.Length;
            RTB.Select(s1, s2 - s1); RTB.SelectedText = text;
        }

        private void OutputError(string errorText)
        {
            text_Out.Text = errorText + ", строка " + (currentRow + (currentWord > 0 ? 1 : 0));
            text_Out.ForeColor = Color.Red; currentRow = currentRow - (currentWord > 0 ? 0 : 1);
            int selectionStartIndex = text_In.GetFirstCharIndexFromLine(currentRow);
            int selectionLength = 1;
            if (currentWord == 0)
            { currentWord = wordsTable[currentRow].Count - 1; }
            else { currentWord--; }
            clearSelection = true;
            if (wordsTable[currentRow][0] == string.Empty)
            {
                changeLine(text_In, currentRow, " ");
                selectionStartIndex = text_In.Find(" ", selectionStartIndex, RichTextBoxFinds.MatchCase);
            }
            else
            {
                for (int i = 0; i <= currentWord; i++)
                {
                    selectionStartIndex = text_In.Find(wordsTable[currentRow][i], selectionStartIndex + (i == 0 ? 0 : 1), RichTextBoxFinds.MatchCase);
                }
                selectionLength = wordsTable[currentRow][currentWord].Length;
            }
            text_In.Select(selectionStartIndex, selectionLength);
            text_In.SelectionColor = System.Drawing.Color.Black;
            text_In.SelectionBackColor = System.Drawing.Color.Red;
        }

        private void PrintData()
        {
            text_Out.Text = "Полученные результаты вычислений";
            foreach (var key in variables.Keys)
            {
                text_Out.Text += Environment.NewLine + key + " = " + variables[key];
            }
            text_Out.ForeColor = Color.Black;
        }

        private bool AnalizeAnnounce()
        {
            bool onePerem = false; //проверка на одну переменную
            if ( wordsTable[currentRow][currentWord] == "Анализ" || CheckFirstWord("Синтез") )
            {
                if(wordsTable[currentRow][currentWord] == "Анализ")
                {
                    currentWord++;
                }
                foreach (List<string> row in wordsTable.Skip(currentRow))
                {
                    foreach (string word in row.Skip(currentWord))
                    {
                        currentWord++;
                        if (word != string.Empty)
                        {
                            if (!onePerem)
                            {
                                VariableErrorType varError = IsVariable(word);
                                if (varError == VariableErrorType.Correct)
                                {
                                    onePerem = true;
                                }
                                else if ((IsDouble(word) == NumericErrorType.Correct))
                                {
                                    OutputError("Ошибка, встречено число. После \"Анализ\" или \"Синтез\" должна быть переменная");
                                    return false;
                                }
                                else if (word == "Анализ" || word == "Синтез" || word == "End")
                                {
                                    OutputError("Ошибка, пропущена переменная после \"Анализ\" или \"Синтез\"");
                                    return false;
                                }
                                else if (varError == VariableErrorType.FirstDigit)
                                {
                                    OutputError("Ошибка, имена переменных должны начинаться с буквы, дана цифра");
                                    return false;
                                }
                                else if (varError == VariableErrorType.FirstUnknownChar)
                                {
                                    OutputError("Ошибка, недопустимый символ при перечеслении переменных");
                                    return false;
                                }
                                else if (varError == VariableErrorType.UnknownChar)
                                {
                                    OutputError("Ошибка, недопустимый символ в имени переменной");
                                    return false;
                                }
                                else if (varError == VariableErrorType.ReserverWord)
                                {
                                    OutputError("Ошибка, переменные не могут быть заданы зарезервированными словами:\r\n\"Begin\"" + "\r\n\"End\"" + "\r\n\"sin\"" + "\r\n\"cos\"" + "\r\n\"tg\"" + "\r\n\"ctg\"");
                                    return false;
                                }
                            }
                            else if (word == "Анализ" || word == "Синтез")
                            {
                                if (onePerem)
                                {
                                    currentWord--;
                                    return true;
                                }
                                else
                                {
                                    OutputError("Ошибка, после \"Анализ\" или \"Синтез\" пропущена одна переменная");
                                    return false;
                                }
                            }
                            else if (word == ",")
                            {
                                semicolon = true;
                                return true;
                            }
                            else if (word == "End")
                            {
                                if(semicolon)
                                {
                                    OutputError("Ошибка, пропущено Множество после знака \";\"");
                                    return false;
                                }
                                else
                                {
                                    currentWord--;
                                    return true;
                                }
                            }
                            else if (IsVariable(word) == VariableErrorType.Correct)
                            {
                                OutputError("Ошибка, должна быть только одна переменная, встречена вторая переменная подряд");
                                return false;
                            }
                            else
                            {
                                OutputError("Ошибка, после \"Анализ\" или \"Синтез\" должна быть переменная");
                                return false;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    currentRow++;
                    currentWord = 0;
                    
                }
                return true;
            }
            else
            {
                OutputError("Ошибка, \"Множество\" должно начинаться со слова \"Анализ\" или \"Синтез\"");
                return false;
            }
        }

        private bool AnalizeAnnounces()
        {
            bool isEndNotFinded = false;
            while (isEndNotFinded = !CheckFirstWord("End"))
            {
                if (currentWord > 0) { currentWord--; }
                if (semicolon)
                {
                    semicolon = false;
                    if (!AnalizeAnnounce()) { return false; }
                    else { poly = true; }
                }
                else
                {
                    OutputError("Ошибка, пропущен знак \",\" между двумя Множествами");
                    return false;
                }
                if (currentRow == wordsTable.Count && (currentWord == wordsTable[currentRow - 1].Count || currentWord == 0)) { break; }
            }
            if (!isEndNotFinded && currentWord > 0) { currentWord--; }
            if (semicolon)
            {
                OutputError("Ошибка, пропущено \"Множество\" после знака \",\"");
                return false;
            }
            else return true;
        }

        private bool AnalizeOperators()
        {
            while ((!(wordsTable[currentRow][currentWord] == "Анализ")) && (!(wordsTable[currentRow][currentWord] == "Синтез")))
            {
                if(wordsTable[currentRow][currentWord] == "End" && yrav==false)
                {
                    currentWord++;
                    OutputError("Ошибка, пропущено \"Уравнение\" после \"Begin\"");
                    return false;
                }
                else if (wordsTable[currentRow][currentWord] == "End" && poly == false)
                {
                    currentWord++;
                    OutputError("Ошибка, пропущено \"Множество\" перед \"End\"");
                    return false;
                }
                if (!AnalizeOperator()) { return false; }
                else { yrav = true; }
                if (currentRow == wordsTable.Count && (currentWord == wordsTable[currentRow - 1].Count || currentWord == 0)) { break; }
            }
            if (yrav)   return AnalizeAnnounces();
            else
            {
                OutputError("Ошибка, пропущено \"Уравнение\" перед \"Множество\"");
                return false;
            }
        }

        private bool AnalizeOperator()
        {
            bool isEmptyAfterDoubleDots = true;
            foreach (List<string> row in wordsTable.Skip(currentRow))
            {
                foreach (string word in row.Skip(currentWord))
                {
                    currentWord++;
                    if (word != string.Empty)
                    {
                        isEmptyAfterDoubleDots = false;
                        VariableErrorType errorType = IsVariable(word);
                        if (errorType == VariableErrorType.Correct)
                        {
                            if (!CheckFirstWord("="))
                            {
                                OutputError("Ошибка, после переменной пропущен знак \"=\"");
                                return false;
                            }
                            string mathResult = CheckAndExecuteMath();
                            if (mathResult != "err")
                            {
                                double value = double.Parse(mathResult);
                                if (variables.ContainsKey(word))
                                {
                                    variables[word] = Convert.ToDouble(value).ToString();
                                }
                                else
                                {
                                    variables.Add(word, Convert.ToDouble(value).ToString());
                                }
                                return true;
                            }
                            else { return false; }
                        }
                        else if (errorType == VariableErrorType.UnknownChar)
                        {
                            OutputError("Ошибка, недопустимый символ в имени переменной");
                            return false;
                        }
                        else if ((IsDouble(word) == NumericErrorType.Correct))
                        {
                            OutputError("Ошибка, только переменным могут быть присвоены значения, дано число");
                            return false;
                        }
                        else if (errorType == VariableErrorType.FirstDigit)
                        {
                            OutputError("Ошибка, имена переменных должны начинаться с буквы, дана цифра");
                            return false;
                        }
                        else if (errorType == VariableErrorType.FirstUnknownChar)
                        {
                            OutputError("Ошибка, математическое выражение содержит недопустимый символ");
                            return false;
                        }
                        else if (errorType == VariableErrorType.ReserverWord)
                        {
                            OutputError("Ошибка, переменные не могут быть заданы зарезервированными словами:\r\n\"Begin\"" + "\r\n\"End\"" + "\r\n\"sin\"" + "\r\n\"cos\"" + "\r\n\"tg\"" + "\r\n\"ctg\"");
                            return false;
                        }
                    }
                }
                currentRow++;
                currentWord = 0;
            }
            if (isEmptyAfterDoubleDots)
            {
                OutputError("Ошибка, после знака ожидалось математическое выражение");
                return false;
            }
            return false;
        }

        private void AnalizeLanguage()
        {
            if (!CheckFirstWord("Begin"))
            {
                OutputError("Ошибка, программа должна начинаться со слова \"Begin\"");
                return;
            }
            else
            {
                if (CheckFirstWord("Анализ"))
                {
                    currentWord--;
                    if (CheckFirstWord("Синтез"))
                    {
                        OutputError("Ошибка, после \"Begin\" дожны быть Уравнения, начинающиеся с переменной");
                        return;
                    }
                    else
                    {
                        OutputError("Ошибка, после \"Begin\" дожны быть Уравнения, начинающиеся с переменной");
                        return;
                    }
                }
                currentWord--;
                if (!AnalizeOperators()) { return; }

            }
            if (CheckFirstWord("End"))
            {
                foreach (List<string> row in wordsTable.Skip(currentRow))
                {
                    foreach (string word in row.Skip(currentWord))
                    {
                        currentWord++;
                        if (word != string.Empty)
                        {
                            OutputError("Ошибка, после слова \"End\" есть текст");
                            return;
                        }
                    }
                    currentRow++;
                    currentWord = 0;
                }
                PrintData();
                return;
            }
            else
            {
                OutputError("Ошибка, программа должна завершаться словом \"End\"");
                return;
            }
        }

        private NumericErrorType IsDouble(string value)
        {
            foreach (char a in value.ToUpper())
            {
                if (a == '.')
                {
                    return NumericErrorType.DoubleNumer;
                }
                else if (a >= '0' && a <= '9')
                {
                    //return NumericErrorType.Correct; 
                }
                else
                {
                    return NumericErrorType.NotNumeric;
                }
            }
            return NumericErrorType.Correct;
            //if (int.TryParse(value, NumberStyles.Any, new CultureInfo("en-US"), out _))
            //{
            //    return NumericErrorType.Correct;
            //}
            //else
            //{
            //    try
            //    {
            //        if (double.TryParse(value, NumberStyles.Any, new CultureInfo("en-US"), out _))
            //        {
            //            return NumericErrorType.DoubleNumer;
            //        }
            //        double val = double.Parse(value, new CultureInfo("en-US"));
            //    }
            //    catch (System.OverflowException)
            //    {
            //        return NumericErrorType.Overflow;
            //    }
            //    catch (System.FormatException)
            //    {
            //        return NumericErrorType.NotNumeric;
            //    }
            //    return NumericErrorType.NotNumeric;
            //}
        }

        private NumericErrorType IsInt(string value)
        {
            if (value.Contains('.'))
            {
                return NumericErrorType.Correct;
            }
            return NumericErrorType.Correct;
        }

        private VariableErrorType IsVariable(string value)
        {
            char a = value.ToUpper()[0];
            if (a >= '0' && a <= '9')
            {
                return VariableErrorType.FirstDigit;
            }
            else if (!((a >= 'A' && a <= 'Z') || (a >= '0' && a <= '9')))
            { return VariableErrorType.FirstUnknownChar; }
            foreach (char c in value.ToUpper())
            {
                if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
                { return VariableErrorType.UnknownChar; }
            }
            if (reservedWords.Contains(value))
            { return VariableErrorType.ReserverWord; }
            return VariableErrorType.Correct;
        }

        private string CheckAndExecuteMath()
        {
            List<string> words = new List<string>();
            bool canCompute = false;
            bool prevPlus = false;//
            bool prevMin = false;//
            bool prevMult = false;// *!/
            bool prevPov = false;
            bool prevDig = false;// число
            bool prevPerem = false;//переменная
            bool prevFunc = false;
            foreach (List<string> row in wordsTable.Skip(currentRow))
            {
                foreach (string word in row.Skip(currentWord))
                {
                    currentWord++;
                    if (word != string.Empty)
                    {
                        if (word == "Анализ" || word == "Синтез")
                        {
                            currentWord--;
                            canCompute = true;
                            break;
                        }
                        else if (word == "End")
                        {
                            currentWord--;
                            canCompute = true;
                            break;
                        }
                        else if (word == "-")
                        {
                            if (prevMin || prevPlus || prevMult || prevPov )
                            {
                                OutputError("Ошибка, два знака математического действия подряд");
                                return "err";
                            }
                            else
                            {
                                words.Add(word);
                                prevMin = true;
                                prevPlus = false;
                                prevMult = false;
                                prevPov = false;
                                prevDig = false;
                                prevPerem = false;
                                prevFunc = false;
                            }
                        }
                        else if (word == "+")
                        {
                            if (!prevDig && !prevPerem )
                            {
                                if (prevMin || prevPlus || prevMult || prevPov )
                                {
                                    OutputError("Ошибка, два знака математического действия подряд");
                                    return "err";
                                }
                                else if (!prevDig || !prevPerem )
                                {
                                    OutputError("Ошибка, знак математического действия \"+\" не может быть в начале выражения");
                                    return "err";
                                }
                            }
                            else if (prevFunc)
                            {
                                OutputError("Ошибка, знак математического действия \"+\" после объявления функции");
                                return "err";
                            }
                            else
                            {
                                words.Add(word);
                                prevMin = false;
                                prevPlus = true;
                                prevMult = false;
                                prevPov = false;
                                prevDig = false;
                                prevPerem = false;
                                prevFunc = false;
                            }
                        }
                        else if (word == "*" || word == "/")
                        {
                            if (!prevDig && !prevPerem )
                            {
                                if (prevMin || prevPlus || prevMult || prevPov )
                                {
                                    OutputError("Ошибка, два знака математического действия подряд");
                                    return "err";
                                }
                                else if (!prevDig || !prevPerem )
                                {
                                    OutputError("Ошибка, знак математического действия \"" + word + "\" в начале выражения правой части");
                                    return "err";
                                }
                            }
                            else if (prevFunc)
                            {
                                OutputError("Ошибка, знак математического действия \"" + word + "\" после объявления функции");
                                return "err";
                            }
                            else
                            {
                                words.Add(word);
                                prevMin = false;
                                prevPlus = false;
                                prevMult = true;
                                prevPov = false;
                                prevDig = false;
                                prevPerem = false;
                                prevFunc = false;
                            }
                        }
                        else if (word == "^")
                        {
                            if (!prevDig && !prevPerem)
                            {
                                if (prevMin || prevPlus || prevMult || prevPov)
                                {
                                    OutputError("Ошибка, два знака математического действия подряд");
                                    return "err";
                                }
                                else if (!prevDig || !prevPerem)
                                {
                                    OutputError("Ошибка, знак математического действия \"" + word + "\" в начале выражения правой части");
                                    return "err";
                                }
                            }
                            else if (prevFunc)
                            {
                                OutputError("Ошибка, степенная операция после объявления функции");
                                return "err";
                            }
                            else
                            {
                                words.Add(word);
                                prevMin = false;
                                prevPlus = false;
                                prevMult = false;
                                prevPov = true;
                                prevDig = false;
                                prevPerem = false;
                                prevFunc = false;
                            }
                        }
                        else if (word == "sin" || word == "cos" || word == "tg" || word == "ctg")
                        {
                            if (prevDig)
                            {
                                OutputError("Ошибка, между числом и функцией пропущен знак действия");
                                return "err";
                            }
                            else if (prevPerem)
                            {
                                OutputError("Ошибка, между переменной и функцией пропущен знак действия");
                                return "err";
                            }
                            else
                            {
                                words.Add(word);
                                prevMin = false;
                                prevPlus = false;
                                prevMult = false;
                                prevPov = false;
                                prevDig = false;
                                prevPerem = false;
                                prevFunc = true;
                            }
                        }
                        else if (IsDouble(word) == NumericErrorType.Correct)
                        {
                            if (prevDig)
                            {
                                OutputError("Ошибка, два числа подряд");
                                return "err";
                            }
                            else if (prevPerem)
                            {
                                OutputError("Ошибка, переменная и число подряд");
                                return "err";
                            } 
                            else
                            {
                                words.Add(word);
                                prevMin = false;
                                prevPlus = false;
                                prevMult = false;
                                prevPov = false;
                                prevDig = true;
                                prevPerem = false;
                                prevFunc = false;
                            }
                        }
                        else if (IsVariable(word) == VariableErrorType.Correct)
                        {
                            if (prevDig || prevPerem)
                            {
                                currentWord--;
                                canCompute = true;
                                prevPerem = true;
                                break;
                            }
                            else if (variables.ContainsKey(word))
                            {
                                words.Add(variables[word]);
                                prevMin = false;
                                prevPlus = false;
                                prevMult = false;
                                prevPov = false;
                                prevDig = false;
                                prevPerem = true;
                                prevFunc = false;
                            }
                            else
                            {
                                OutputError("Ошибка, обращение к неинициализированной переменной");
                                return "err";
                            }
                        }
                        else if (IsDouble(word) == NumericErrorType.DoubleNumer)
                        {
                            OutputError("Ошибка, могут быть заданы только целые числа, использовано вещественное число");
                            return "err";
                        }
                        else if (IsVariable(word) == VariableErrorType.FirstDigit)
                        {
                            OutputError("Ошибка, имена переменных должны начинаться с буквы, дана цифра");
                            return "err";
                        }
                        else if (IsVariable(word) == VariableErrorType.UnknownChar)
                        {
                            OutputError("Ошибка, недопустимый символ в имени переменной");
                            return "err";
                        }
                        else if (IsVariable(word) == VariableErrorType.ReserverWord)
                        {
                            OutputError("Ошибка, переменные не могут быть заданы зарезервированными словами:\r\n\"Begin\"" + "\r\n\"End\"" + "\r\n\"sin\"" + "\r\n\"cos\"" + "\r\n\"tg\"" + "\r\n\"ctg\"");
                            return "err";
                        }
                        else if (IsVariable(word) == VariableErrorType.FirstUnknownChar)
                        {
                            OutputError("Ошибка, выражение содержит недопустимый символ");
                            return "err";
                        }
                        else
                        {
                            OutputError("Ошибка, выражение содержит недопустимый символ");
                            return "err";
                        }
                    }
                }
                if (canCompute) { break; }
                else
                {
                    currentRow++; currentWord = 0;
                }
            }
            if (prevMin || prevPlus || prevPov || prevMult || prevFunc)
            {
                OutputError("Ошибка, после знака действия должны идти переменная или целое число");
                return "err";
            }
            else if (words.Count < 1)
            {
                OutputError("Ошибка, после знака \"=\" ожидалось выражение");
                return "err";
            }
            return ComputeMath(ComputeBrackets(string.Join(" ", words.ToArray())));
        }

        private string ComputeMath(string math)
        {
            RegexOptions options = RegexOptions.None;
            Regex regex = new Regex("[ ]{2,}", options);
            math = regex.Replace(math, " ").Replace(",", ".").Replace("cos", "").Replace("sin", "").Replace("tg", "").Replace("ctg", "");
            math = Regex.Replace(math, @"\d+(\.\d+)?", m => { var x = m.ToString(); return x.Contains(".") ? x : string.Format("{0}.0", x); });
            math = ComputePov(math).Replace(",", ".");
            string result = "err";
            try
            {
                result = String.Format("{0:F20}", Convert.ToDouble(new DataTable().Compute(math, "")));
            }
            catch (System.OverflowException)
            {
                OutputError("Возникла ошибка в процессе вычислений. Полученные вычисления превысели Int64");
            }
            catch (System.DivideByZeroException)
            {
                OutputError("Возникла ошибка в процессе вычислений. Деление на ноль");
            }
            catch (System.Data.EvaluateException)
            {
                OutputError("Возникла ошибка в процессе вычислений. Полученные вычисления превысели Int64");
            }
            return result;
        }

        private string ComputeBrackets(string math)
        {
            while (math.Contains("("))
            {
                string beforeOpen = math.Substring(0, math.IndexOf("("));
                string afterOpen = math.Substring(math.IndexOf("(") + 1);
                if (afterOpen.IndexOf("(") < afterOpen.IndexOf(")"))
                {
                    afterOpen = ComputeBrackets(afterOpen);
                    string inBrackets = afterOpen.Substring(0, afterOpen.IndexOf(")"));
                    afterOpen = afterOpen.Substring(afterOpen.IndexOf(")") + 1);
                    inBrackets = ComputeMath(inBrackets);
                    math = beforeOpen + inBrackets + afterOpen;
                }
                else
                {
                    string inBrackets = afterOpen.Substring(0, afterOpen.IndexOf(")"));
                    afterOpen = afterOpen.Substring(afterOpen.IndexOf(")") + 1);
                    inBrackets = ComputeMath(inBrackets);
                    math = beforeOpen + inBrackets + afterOpen;
                }
            }
            return math;
        }

        private string ComputePov(string math)
        {
            if (math.Contains("^"))
            {
                string[] wordsForPov = math.Split(' ');
                for (int j = wordsForPov.Length - 1; j > 0; j--)
                {
                    wordsForPov[j] = wordsForPov[j].Trim();
                    if (wordsForPov[j] == "^")
                    {
                        if (j == 0 || (j + 1) == wordsForPov.Length)
                        { continue; }
                        else if (IsInt(wordsForPov[j - 1]) == NumericErrorType.Correct && IsInt(wordsForPov[j + 1]) == NumericErrorType.Correct)
                        {
                            var answer = Math.Pow(double.Parse(wordsForPov[j - 1], new CultureInfo("en-US")), double.Parse(wordsForPov[j + 1], new CultureInfo("en-US")));
                            wordsForPov[j - 1] = String.Format("{0:F20}", answer).Replace(",", ".");
                            wordsForPov[j + 1] = wordsForPov[j] = "";
                        }
                    }
                }
                RegexOptions options = RegexOptions.None;
                Regex regex = new Regex("[ ]{2,}", options);
                math = regex.Replace(string.Join(" ", wordsForPov), " ");
            }
            return math;
        }

        private void text_In_MouseDown(object sender, MouseEventArgs e)
        {
            if (clearSelection)
            {
                text_In.SelectAll();
                text_In.SelectionColor = System.Drawing.Color.Black;
                text_In.SelectionBackColor = System.Drawing.Color.White;
                text_In.DeselectAll(); clearSelection = false;
            }
        }
    }
}
