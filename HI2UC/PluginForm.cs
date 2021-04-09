﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Nikse.SubtitleEdit.PluginLogic.Commands;
using Nikse.SubtitleEdit.PluginLogic.Strategies;

namespace Nikse.SubtitleEdit.PluginLogic
{
    internal partial class PluginForm : Form, IConfigurable
    {
        // private static readonly Color Color = Color.FromArgb(41, 57, 85);
        // private readonly LinearGradientBrush _gradientBrush;
        public string Subtitle { get; private set; }
        private readonly Subtitle _subtitle;

        //private Dictionary<string, string> _fixedTexts = new Dictionary<string, string>();
        private HiConfigs _hiConfigs;

        //private Context _hearingImpaired;
        private readonly bool _isLoading;

        public PluginForm(Subtitle subtitle, string name, string description)
        {
            InitializeComponent();

            //SetControlColor(this);
            // _gradientBrush = new LinearGradientBrush(ClientRectangle, Color, Color.White, 0f);

            Text = $@"{name} - v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(2)}";

            _subtitle = subtitle;
            labelDesc.Text = @"Description: " + description;

            LoadConfigurations();
            FormClosed += (s, e) =>
            {
                // update config
                //_hiConfigs.Convert = checkBoxNames.Checked;
                //_hiConfigs.MoodsToUppercase = checkBoxMoods.Checked;
                //_hiConfigs.RemoveExtraSpaces = checkBoxRemoveSpaces.Checked;
                //_hiConfigs.Style = (HIStyle)Enum.Parse(typeof(HIStyle), comboBoxStyle.SelectedValue.ToString());
                //_hiConfigs.Style = ((ComboBoxItem)comboBoxStyle.SelectedItem).Style;
                // TypeConverter converter = TypeDescriptor.GetConverter(typeof(HIStyle));
                _hiConfigs.NarratorToUppercase = checkBoxNames.Checked;
                _hiConfigs.MoodsToUppercase = checkBoxMoods.Checked;
                _hiConfigs.RemoveExtraSpaces = checkBoxRemoveSpaces.Checked;
                SaveConfigurations();
            };

            Resize += delegate { listViewFixes.Columns[listViewFixes.Columns.Count - 1].Width = -2; };

            linkLabel1.DoubleClick += LinkLabel1_DoubleClick;

            // force layout
            OnResize(EventArgs.Empty);

            UpdateUiFromConfigs(_hiConfigs);
            InitComboBoxHiStyle();

            _isLoading = false;
            GeneratePreview();

            checkBoxMoods.CheckedChanged += GeneratePreviewOnChangeState;
            checkBoxNames.CheckedChanged += GeneratePreviewOnChangeState;
            checkBoxRemoveSpaces.CheckedChanged += GeneratePreviewOnChangeState;
            comboBoxStyle.SelectedIndexChanged += GeneratePreviewOnChangeState;

            // donate handler
            pictureBoxDonate.Click += delegate { Process.Start(StringUtils.DonateUrl); };
        }

        private void GeneratePreviewOnChangeState(object sender, EventArgs e)
        {
            GeneratePreview();
            ;
        }

        private void LinkLabel1_DoubleClick(object sender, EventArgs e)
        {
            Process.Start("https://github.com/SubtitleEdit/plugins/issues/new");
        }

        private void UpdateUiFromConfigs(HiConfigs configs)
        {
            checkBoxRemoveSpaces.Checked = configs.RemoveExtraSpaces;
            checkBoxNames.Checked = configs.NarratorToUppercase;
            checkBoxMoods.Checked = configs.MoodsToUppercase;
        }

        private void InitComboBoxHiStyle()
        {
            // ReSharper disable once CoVariantArrayConversion
            comboBoxStyle.Items.AddRange(GetStrategies());
            comboBoxStyle.SelectedIndex = 1; // set default selected to titlecase
        }


        private ICaseStrategy[] GetStrategies()
        {
            // note: register the strategies here
            return new ICaseStrategy[]
            {
                new NoneCaseStrategy(),
                new SentenceCaseStrategy(CultureInfo.CurrentCulture),
                new UpperCaseStrategy(CultureInfo.CurrentCulture),
                new LowerCaseStrategy(CultureInfo.CurrentCulture),
                new TongleCaseStrategy(CultureInfo.CurrentCulture),
                new StartCaseStrategy(CultureInfo.CurrentCulture)
            };
        }

        private void Btn_Cancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void Btn_Run_Click(object sender, EventArgs e)
        {
            if (listViewFixes.Items.Count > 0)
            {
                Cursor = Cursors.WaitCursor;
                listViewFixes.Resize -= ListViewFixes_Resize;
                ApplyChanges();
                Cursor = Cursors.Default;
            }

            Subtitle = _subtitle.ToText();
            DialogResult = DialogResult.OK;
        }

        private void ApplyChanges()
        {
            if (_subtitle?.Paragraphs == null || _subtitle.Paragraphs.Count == 0)
            {
                return;
            }

            listViewFixes.BeginUpdate();
            var map = _subtitle.Paragraphs.ToDictionary(p => p.Id);
            foreach (ListViewItem listViewItem in listViewFixes.Items)
            {
                if (!listViewItem.Checked)
                {
                    continue;
                }

                var record = (Record) listViewItem.Tag;
                // record.Paragraph.Text = record.After;
                
                // udate origilna subtilte
                map[record.Paragraph.Id].Text = record.After;
            }

            listViewFixes.EndUpdate();
        }

        private void CheckBoxNarrator_CheckedChanged(object sender, EventArgs e)
        {
            GeneratePreview();
        }

        private void CheckTypeStyle(object sender, EventArgs e)
        {
            if (listViewFixes.Items.Count <= 0 || !(sender is ToolStripMenuItem menuItem))
            {
                return;
            }

            switch (menuItem.Text)
            {
                case "Check all":
                {
                    for (int i = 0; i < listViewFixes.Items.Count; i++)
                    {
                        listViewFixes.Items[i].Checked = true;
                    }

                    break;
                }
                case "Uncheck all":
                {
                    for (int i = 0; i < listViewFixes.Items.Count; i++)
                    {
                        listViewFixes.Items[i].Checked = false;
                    }

                    break;
                }
                case "Invert check":
                {
                    for (int i = 0; i < listViewFixes.Items.Count; i++)
                    {
                        listViewFixes.Items[i].Checked = !listViewFixes.Items[i].Checked;
                    }

                    break;
                }
                case "Copy":
                {
                    string text = ((Paragraph) listViewFixes.FocusedItem.Tag).ToString();
                    Clipboard.SetText(text, TextDataFormat.UnicodeText);
                    break;
                }
                default: // remove selection
                {
                    for (int idx = listViewFixes.SelectedIndices.Count - 1; idx >= 0; idx--)
                    {
                        int index = listViewFixes.SelectedIndices[idx];
                        if (listViewFixes.Items[idx].Tag is Record record)
                        {
                            _subtitle.RemoveLine(record.Paragraph.Number);
                        }

                        listViewFixes.Items.RemoveAt(index);
                    }

                    _subtitle.Renumber();
                    break;
                }
            }
        }

        private void GeneratePreview()
        {
            if (_isLoading)
            {
                return;
            }

            listViewFixes.BeginUpdate();
            listViewFixes.Items.Clear();

            var controller = new CaseController();

            // copy current subtitle
            var subtilte = new Subtitle(_subtitle);

            foreach (IStyleCommand command in GetCommands())
            {
                command.Convert(subtilte.Paragraphs, controller);
            }
            // _subtitle.Paragraphs.Zip(subtilte, (p1, p2) => p1.Text.Equals(p2.Text, StringComparison.Ordinal) ? p2.Text, null).Where()

            // foreach (var record in controller.Records.OrderBy(r => r.Paragraph.Number))
            // note: the line below  fixes one of the interesting problem...
            // to see the issue here.. comment the line below and try running with the foreach above.
            // since the commands are being runnig sequencial, the narrator => will change the narrator and report it as record and the Mood command will
            // also run on the same paragraph and report it. which will add two record on the list view.. but only the last report is relevant in this case
            // so try to group same paragrpah and select the last one
            foreach (var record in controller.Records.GroupBy(r => r.Paragraph.Id).Select(group => group.Last()).OrderBy(r => r.Paragraph.Number))
            {
                var item = new ListViewItem(string.Empty) {Checked = true};
                item.SubItems.Add(record.Paragraph.Number.ToString());
                item.SubItems.Add(StringUtils.GetListViewString(record.Before));
                item.SubItems.Add(StringUtils.GetListViewString(record.After));
                item.Tag = record;
                listViewFixes.Items.Add(item);
            }

            //groupBox1.ForeColor = totalConvertParagraphs <= 0 ? Color.Red : Color.Green;
            //groupBox1.Text = $@"Total Found: {totalConvertParagraphs}";
            /*this.listViewFixes.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            this.listViewFixes.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);*/
            //Application.DoEvents();
            listViewFixes.EndUpdate();
            Refresh();
        }

        private void ButtonApply_Click(object sender, EventArgs e)
        {
            if (listViewFixes.Items.Count == 0)
            {
                return;
            }

            Cursor = Cursors.WaitCursor;
            listViewFixes.Resize -= ListViewFixes_Resize;
            // TODO: fix when there are both narrator and mood at the same line.
            // if you click apploy the line will be applied with e.g:
            // [foobar]. narrator: hell! => [foobar]. NARRATOR: hello!
            // [FOOBAR]. narrator: hell! => [FOOBAR]. narrator: hello!
            // output =>  [FOOBAR]. narrator: hello!
            // expected: [FOOBAR]. NARRATOR: hello!
            ApplyChanges();
            GeneratePreview();
            listViewFixes.Resize += ListViewFixes_Resize;
            Cursor = Cursors.Default;
        }

        private void ListViewFixes_Resize(object sender, EventArgs e)
        {
            int newWidth = (listViewFixes.Width - (listViewFixes.Columns[0].Width + listViewFixes.Columns[1].Width)) /
                           2;
            listViewFixes.Columns[2].Width = newWidth;
            listViewFixes.Columns[3].Width = newWidth;
        }

        private ICollection<IStyleCommand> GetCommands()
        {
            var commands = new List<IStyleCommand>();
            if (checkBoxRemoveSpaces.Checked)
            {
                commands.Add(new RemoveExtraSpaceStyleCommand());
            }

            if (checkBoxMoods.Checked)
            {
                commands.Add(new StyleMoodsStyleCommand(GetStrategy()));
            }

            if (checkBoxNames.Checked)
            {
                commands.Add(new StyleNarratorStyleCommand(GetStrategy()));
            }

            return commands;
        }

        private ICaseStrategy GetStrategy() => (ICaseStrategy) comboBoxStyle.SelectedItem;

        public void LoadConfigurations()
        {
            string configFile = Path.Combine(FileUtils.Plugins, "hi2uc-config.xml");

            // load from existing file
            if (File.Exists(configFile))
            {
                _hiConfigs = HiConfigs.LoadConfiguration(configFile);
            }
            else
            {
                _hiConfigs = new HiConfigs(configFile);
                _hiConfigs.SaveConfigurations();
            }

            //_hearingImpaired = new Context(_hiConfigs);
        }

        public void SaveConfigurations()
        {
            _hiConfigs.SaveConfigurations();
        }

        private void PluginForm_Paint(object sender, PaintEventArgs e)
        {
            // ignore form painting operation
            return;
            //e.Graphics.FillRectangle(gradientBrush, ClientRectangle);
        }

        private void ListViewFixes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewFixes.SelectedItems.Count == 0)
            {
                return;
            }

            // todo: get updated text from text-box

            //ListViewItem selItem = listViewFixes.SelectedItems[0];
            //textBoxParagraphText.Text = _fixedTexts[((Paragraph)selItem.Tag).Id];
            //selItem.SubItems[4].Text = _fixedTexts[((Paragraph)selItem.Tag).Id];

            return;

            //textBoxParagraphText.DataBindings.Clear();
            //var selParagraph = selItem.Tag as Paragraph;

            // bind Textbox's text property to selected paragraph in listview
            //textBoxParagraphText.DataBindings.Add("Text", selParagraph, "Text");
            // at this point paragraph's text property will be update, then use the updated text to update update _fixtedtext dictionary
            //_fixedTexts[selParagraph.Id] = selParagraph.Text;
            //textBoxParagraphText.DataBindings.Add("Text", selItem.SubItems[3], "Text", false, DataSourceUpdateMode.OnPropertyChanged);

            // todo: issues
            // when set italic/bold/underline is clicked it will use listview.subitems text 
            // to update _fixedTexts...
            // the binding is becoming complicated at this point, because we bound Paragraph.Text => textBoxParagraphText.Text
        }

        private void SetTag(string tag)
        {
            if (listViewFixes.SelectedItems.Count == 0)
            {
                return;
            }

            listViewFixes.BeginUpdate();
            //string closeTag = $"</{tag[1]}>";
            //foreach (ListViewItem lvi in listViewFixes.SelectedItems)
            //{
            //    var p = (Paragraph)lvi.Tag;
            //    string value = _fixedTexts[p.Id];
            //    value = HtmlUtils.RemoveOpenCloseTags(value, tag[1].ToString());
            //    value = $"{tag}{value}{closeTag}";
            //    // refresh fixed values
            //    lvi.SubItems[AfterTextIndex].Text = StringUtils.GetListViewString(value, noTag: false);
            //    _fixedTexts[p.Id] = value;
            //}

            //if (listViewFixes.SelectedItems.Count > 0)
            //{
            //    textBoxParagraphText.Text = _fixedTexts[((Paragraph)listViewFixes.SelectedItems[0].Tag).Id];
            //}

            listViewFixes.EndUpdate();
        }
    }
}