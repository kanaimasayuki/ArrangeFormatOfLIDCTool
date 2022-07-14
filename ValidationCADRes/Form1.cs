using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ValidationCADRes
{
    public partial class Form1 : Form
    {
        

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //[ファイルの種類]をrawに設定する
            openFileDialog1.Filter = "rawデータ(*.raw)|*.raw";

            //ダイアログを表示する
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = System.IO.Path.GetDirectoryName(openFileDialog1.FileName);
                
            }
            else
            {
                return;
            }

            return;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //[ファイルの種類]をrawに設定する
            openFileDialog1.Filter = "rawデータ(*.raw)|*.raw";

            //ダイアログを表示する
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox2.Text = System.IO.Path.GetDirectoryName(openFileDialog1.FileName);
                
            }
            else
            {
                return;
            }

            return;
        }



        private void button3_Click(object sender, EventArgs e)
        {
            IEnumerable<string> LIDCfiles = null;
            IEnumerable<string> CADfiles = null;
            LIDCfiles =
                    System.IO.Directory.EnumerateFiles(
                    textBox1.Text, "*.raw", System.IO.SearchOption.AllDirectories);

            CADfiles =
                    System.IO.Directory.EnumerateFiles(
                    textBox2.Text, "*.raw", System.IO.SearchOption.AllDirectories);

            //CSVファイルに書き込むときに使うEncoding
            System.Text.Encoding enc =
                System.Text.Encoding.GetEncoding("Shift_JIS");

            //書き込むファイルを開く
            System.IO.StreamWriter sr =null;
            try
            {
                sr = new System.IO.StreamWriter("LIDCresults.csv", false, enc);
                sr.WriteLine("#, TP, FP, FN, LesionNum");
                sr.Close();
            }
            catch (Exception)
            {
            }
            finally
            {
                sr.Close();
            }

            int filecount = 0;
            foreach (string file1 in LIDCfiles)
            {
                filecount++;
                string fn1 = System.IO.Path.GetFileName(file1);
                //file1に相当するファイルを探索
                foreach (string file2 in CADfiles)
                {
                    string fn2 = System.IO.Path.GetFileName(file2);
                    //同じファイル名だったら
                    if ("out_"+fn1 == fn2)
                    {
                        //比較する
                        var CC = new CompareClass(file1, file2);

                        //ファイルに書き込む
                        System.IO.StreamWriter ssr = null;
                        try
                        {
                            ssr = new System.IO.StreamWriter("LIDCresults.csv", true, enc);
                            ssr.WriteLine("{0}, {1}, {2}, {3}, {4}", filecount, CC.tp, CC.fp, CC.fn, CC.lesionNum);
                        }
                        catch (Exception)
                        {
                        }
                        finally
                        {
                            ssr.Close();
                        }

                        break;
                    }
                }
            }

        }



    }
}
