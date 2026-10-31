using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;
using DexManager.Services;
using DexManager.Utils;
namespace DexManager.Forms
{
 [DataContract] internal sealed class HelpDocument { [DataMember(Name="topics")] public List<HelpTopic> Topics {get;set;} }
 [DataContract] internal sealed class HelpTopic {
  [DataMember(Name="title")] public string Title {get;set;}
  [DataMember(Name="summary")] public string Summary {get;set;}
  [DataMember(Name="blocks")] public List<HelpBlock> Blocks {get;set;}
 }
 [DataContract] internal sealed class HelpBlock {
  [DataMember(Name="type")] public string Type {get;set;}
  [DataMember(Name="heading")] public string Heading {get;set;}
  [DataMember(Name="text")] public string Text {get;set;}
  [DataMember(Name="items")] public List<string> Items {get;set;}
 }
 public sealed class HelpForm : Form
 {
  private readonly RoundedPanel _navigation, _content;
  private readonly FlowLayoutPanel _menu, _article;
  private readonly ThemedButton _close;
  private readonly List<ThemedButton> _topics = new List<ThemedButton>();
  private readonly HelpDocument _document;
  private readonly Label _title, _subtitle;
  private DocumentationForm _guide;
  private int _selected;
  public HelpForm()
  {
   Text="DX Manager · "+StoreUiText.Get("HelpCenterHeading.Text","Help"); Icon=AppIconProvider.Current;
   Font=UiFonts.Pixels(14); DoubleBuffered=true; AutoScaleMode=AutoScaleMode.Dpi; AutoScaleDimensions=new SizeF(96,96);
   StartPosition=FormStartPosition.CenterParent; UiWindowStyle.ApplyStoreSize(this,1040,760);
   FormBorderStyle=FormBorderStyle.Sizable; MaximizeBox=true; MinimumSize=new Size(760,560);
   using(var stream=Resource("help.json")) _document=(HelpDocument)new DataContractJsonSerializer(typeof(HelpDocument)).ReadObject(stream);
   _navigation=new RoundedPanel {Radius=20,BackColor=Color.Transparent};
   _content=new RoundedPanel {Radius=20,BackColor=Color.Transparent};
   _title=new UiLabel {Text=StoreUiText.Get("HelpCenterHeading.Text","Help · Troubleshooting"),Font=UiFonts.Pixels(18,true),BackColor=Color.Transparent,Tag="primary"};
   _subtitle=new UiLabel {Text=StoreUiText.Get("HelpCenterSubtitle.Text","Important usage and recovery steps"),Font=UiFonts.Pixels(13),BackColor=Color.Transparent};
   _menu=new BufferedFlowPanel {AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Color.Transparent};
   _article=new BufferedFlowPanel {AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Color.Transparent,Padding=new Padding(0,0,8,16)};
   _navigation.Controls.AddRange(new Control[]{_title,_subtitle,_menu}); _content.Controls.Add(_article);
   ThemedScrollBar.Attach(_menu);ThemedScrollBar.Attach(_article);
   var symbols=new[]{UiSymbol.Folder,UiSymbol.Display,UiSymbol.Link,UiSymbol.List,UiSymbol.Power,UiSymbol.Information};
   for(int i=0;i<_document.Topics.Count;i++) {
    int index=i; var topic=_document.Topics[i];
    var button=new ThemedButton {Text=topic.Title,SecondaryText=topic.Summary,MenuCardStyle=true,Symbol=symbols[i],Size=new Size(278,74),Margin=new Padding(0,0,0,8),CornerRadius=6};
    button.Click+=delegate {ShowTopic(index);}; _topics.Add(button); _menu.Controls.Add(button);
   }
   var detailed=new ThemedButton {Text=StoreUiText.Get("HelpDetailedGuideTitle","User guide · FAQ"),SecondaryText=LocalizationService.IsKorean?"전체 사용 방법과 자주 묻는 질문":"Full usage guide and frequently asked questions",MenuCardStyle=true,Symbol=UiSymbol.Information,Size=new Size(278,74),Margin=new Padding(0,0,0,8),CornerRadius=6};
   detailed.Click+=delegate { if(_guide==null || _guide.IsDisposed) _guide=new DocumentationForm(); _guide.Show(); _guide.Activate(); };
   _topics.Add(detailed); _menu.Controls.Add(detailed);
   _close=new ThemedButton {Primary=true,Text=StoreUiText.Get("HelpCenterCloseButton.Content",LocalizationService.Get("Common.Close")),CornerRadius=10}; _close.Click+=delegate {Close();};
   Controls.AddRange(new Control[]{_navigation,_content,_close}); Resize+=delegate {LayoutHelp();};
   FormClosed+=delegate {if(_guide!=null) _guide.Dispose();}; LayoutHelp(); ShowTopic(0); ApplyCurrentTheme();
  }
  internal static Stream Resource(string file) {return Assembly.GetExecutingAssembly().GetManifestResourceStream("DexManager.Resources.Help."+(LocalizationService.IsKorean?"ko":"en")+"."+file);}
  protected override void OnPaintBackground(PaintEventArgs e) {DesktopSurface.Paint(e.Graphics,ClientRectangle);}
  private float ScaleValue {get {return Math.Max(1F,CurrentAutoScaleDimensions.Width/96F);}}
  private void LayoutHelp() {
   if(_navigation==null) return; float s=ScaleValue;
   _navigation.Bounds=new Rectangle((int)(28*s),(int)(16*s),(int)(300*s),ClientSize.Height-(int)(96*s));
   _content.Bounds=new Rectangle((int)(344*s),(int)(16*s),ClientSize.Width-(int)(372*s),ClientSize.Height-(int)(96*s));
   _title.Bounds=new Rectangle((int)(14*s),(int)(28*s),(int)(274*s),(int)(26*s));
   _subtitle.Bounds=new Rectangle((int)(14*s),(int)(57*s),(int)(274*s),(int)(24*s));
   _menu.Bounds=new Rectangle((int)(10*s),(int)(94*s),_navigation.Width-(int)(20*s),_navigation.Height-(int)(104*s));
   foreach(var button in _topics) button.Size=new Size(Math.Max(100,_menu.ClientSize.Width-4),(int)(74*s));
   _article.Bounds=new Rectangle((int)(24*s),(int)(24*s),_content.Width-(int)(48*s),_content.Height-(int)(48*s));
   _close.Bounds=new Rectangle(ClientSize.Width-(int)(138*s),ClientSize.Height-(int)(60*s),(int)(110*s),(int)(40*s));
   if(_document!=null && _article.Controls.Count>0) ShowTopic(_selected);
  }
  private Label BlockText(string text,float size,bool semi,int bottom=14) {
   return new UiLabel {Text=text??"",AutoSize=true,MaximumSize=new Size(Math.Max(120,_article.Width-24),0),Font=UiFonts.Pixels(size,semi),BackColor=Color.Transparent,Tag=semi?"primary":null,Margin=new Padding(0,0,0,bottom)};
  }
  private void ShowTopic(int index) {
   using(UiRendering.Freeze(_content)) {
   _selected=index; _article.SuspendLayout();
   while(_article.Controls.Count>0) _article.Controls[0].Dispose();
   for(int i=0;i<_topics.Count;i++) _topics[i].Primary=i==index;
   var topic=_document.Topics[index]; _article.Controls.Add(BlockText(topic.Title,26,true,6)); _article.Controls.Add(BlockText(topic.Summary,13,false,18));
   foreach(var block in topic.Blocks) {
    if(block.Type=="warning") {
     var warning=new RoundedPanel {Radius=12,Width=Math.Max(120,_article.Width-24),FillColor=ThemeColors.Current.AccentSoft,BorderColor=ThemeColors.Current.Accent,Margin=new Padding(0,0,0,14),Padding=new Padding(14,12,14,12),AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Tag="warning"};
     warning.MinimumSize=new Size(warning.Width,0); warning.MaximumSize=new Size(warning.Width,0);
     var stack=new BufferedFlowPanel {Location=new Point(14,12),Size=new Size(warning.Width-28,1),MinimumSize=new Size(warning.Width-28,0),MaximumSize=new Size(warning.Width-28,0),Margin=Padding.Empty,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Color.Transparent};
     var heading=BlockText("⚠  "+block.Heading,15,true,6); heading.MaximumSize=new Size(stack.Width,0);
     var text=BlockText(block.Text,15,false,0); text.MaximumSize=new Size(stack.Width,0);
     stack.Controls.AddRange(new Control[]{heading,text}); warning.Controls.Add(stack); _article.Controls.Add(warning); continue;
    }
    if(!string.IsNullOrEmpty(block.Heading)) _article.Controls.Add(BlockText(block.Heading,18,true,10));
    if(block.Type=="steps" && block.Items!=null) {
     int n=0; foreach(var item in block.Items) {
      var row=new Panel {Width=Math.Max(120,_article.Width-24),BackColor=Color.Transparent,Margin=new Padding(0,0,0,10)};
      var badge=new RoundedPanel {Radius=13,Size=new Size(26,26),FillColor=ThemeColors.Current.AccentSoft,BorderColor=ThemeColors.Current.AccentSoft};
      badge.Controls.Add(new UiLabel {Text=(++n).ToString(),Font=UiFonts.Pixels(15,true),ForeColor=ThemeColors.Current.Accent,TextAlign=ContentAlignment.MiddleCenter,Dock=DockStyle.Fill,BackColor=Color.Transparent,Tag="accent"});
      var text=BlockText(item,15,false,0); text.MaximumSize=new Size(row.Width-36,0); text.Location=new Point(36,0);
      row.Controls.AddRange(new Control[]{badge,text}); row.Height=Math.Max(26,text.GetPreferredSize(new Size(row.Width-36,0)).Height); _article.Controls.Add(row);
     }
    } else if(!string.IsNullOrEmpty(block.Text)) _article.Controls.Add(BlockText(block.Text,15,false));
   }
   _article.AutoScrollPosition=Point.Empty; _article.ResumeLayout(); ApplyCurrentTheme();
   }
  }
  public void ApplyCurrentTheme() {
   var t=ThemeColors.Current; BackColor=t.WindowBackground;
   foreach(var card in new[]{_navigation,_content}) {card.FillColor=t.CardBackground;card.BorderColor=t.CardBorder;}
   ThemeTree(_navigation); ThemeTree(_content); if(_guide!=null && !_guide.IsDisposed) _guide.ApplyCurrentTheme(); Invalidate(true);
  }
  private static void ThemeTree(Control parent) {
   var t=ThemeColors.Current;
   foreach(Control c in parent.Controls) {
    if(c is Label) c.ForeColor=(c.Tag as string)=="accent"?t.Accent:(c.Tag as string)=="primary"?t.TextPrimary:t.TextSecondary;
    var card=c as RoundedPanel; if(card!=null) {card.FillColor=t.AccentSoft;card.BorderColor=(c.Tag as string)=="warning"?t.Accent:t.AccentSoft;}
    c.Invalidate(); if(c.HasChildren) ThemeTree(c);
   }
  }
 }
 public sealed class DocumentationForm : Form, IMessageFilter {
  private readonly RoundedPanel _card;
  private readonly Panel[] _pages=new Panel[2];
  private readonly RichTextBox[] _documents=new RichTextBox[2];
  private readonly ThemedButton[] _tabs=new ThemedButton[2];
  private readonly Label _heading,_summary;
  private readonly ThemedButton _close;
  private int _selectedDocument = -1;
  private readonly int[] _documentOffsets = new int[2];
  public DocumentationForm() {
   Text="DX Manager · "+StoreUiText.Get("HelpDetailedGuideTitle","User guide · FAQ");Icon=AppIconProvider.Current;Font=UiFonts.Pixels(14);
   DoubleBuffered=true;UiWindowStyle.ApplyStoreSize(this,920,720);FormBorderStyle=FormBorderStyle.Sizable;MaximizeBox=true;MinimumSize=new Size(660,480);
   _heading=new UiLabel {Text=LocalizationService.IsKorean?"DX Manager · 사용 설명서":"DX Manager · User guide",Font=UiFonts.Pixels(28,true),BackColor=Color.Transparent};
   _summary=new UiLabel {Text=LocalizationService.IsKorean?"현재 포터블 버전의 사용 설명서와 자주 묻는 질문입니다.":"Offline user guide and FAQ for the current portable version.",Font=UiFonts.Pixels(13),BackColor=Color.Transparent};
   _card=new RoundedPanel {Radius=20,Padding=new Padding(24),BackColor=Color.Transparent};
   for(int i=0;i<2;i++) {
    int index=i;_tabs[i]=new ThemedButton {Text=i==0?(LocalizationService.IsKorean?"사용 설명서":"User guide"):(LocalizationService.IsKorean?"자주 묻는 질문":"FAQ"),CornerRadius=6};_tabs[i].Click+=delegate {SwitchDocument(index);};
    _pages[i]=new Panel {AutoScroll=true,BackColor=ThemeColors.Current.CardBackground};
    _documents[i]=new RichTextBox {ReadOnly=true,BorderStyle=BorderStyle.None,Font=UiFonts.Pixels(14),ScrollBars=RichTextBoxScrollBars.None,WordWrap=true,DetectUrls=false};
    using(var stream=HelpForm.Resource(i==0?"guide.txt":"faq.txt"))using(var reader=new StreamReader(stream))_documents[i].Text=reader.ReadToEnd();
    _pages[i].Controls.Add(_documents[i]);_card.Controls.Add(_pages[i]);_card.Controls.Add(_tabs[i]);ThemedScrollBar.Attach(_pages[i]);
   }
   _close=new ThemedButton {Text=LocalizationService.Get("Common.Close"),CornerRadius=6};_close.Click+=delegate {Close();};
   Controls.AddRange(new Control[]{_card,_heading,_summary,_close});Resize+=delegate {LayoutDocuments();};Shown+=delegate {LayoutDocuments();};
   LayoutDocuments();SwitchDocument(0);ApplyCurrentTheme();
   Application.AddMessageFilter(this);
   FormClosed+=delegate {Application.RemoveMessageFilter(this);};
  }
  protected override void OnPaintBackground(PaintEventArgs e){DesktopSurface.Paint(e.Graphics,ClientRectangle);}
  private void LayoutDocuments() {
   if(_card==null)return;
   _heading.Bounds=new Rectangle(28,18,ClientSize.Width-56,38);_summary.Bounds=new Rectangle(28,60,ClientSize.Width-56,26);
   _card.Bounds=new Rectangle(24,104,ClientSize.Width-48,ClientSize.Height-176);_close.Bounds=new Rectangle(ClientSize.Width-134,ClientSize.Height-58,110,40);
   for(int i=0;i<2;i++) {
    _tabs[i].Bounds=new Rectangle(24+i*160,24,150,36);_pages[i].Bounds=new Rectangle(24,76,_card.Width-48,_card.Height-100);
    int offset=-_pages[i].AutoScrollPosition.Y;
    _documents[i].Bounds=new Rectangle(2,0,Math.Max(200,_pages[i].ClientSize.Width-24),30000);
    var handle=_documents[i].Handle;
    var last=_documents[i].GetPositionFromCharIndex(Math.Max(0,_documents[i].TextLength-1));
    _documents[i].Height=Math.Max(_pages[i].ClientSize.Height,last.Y+_documents[i].Font.Height+24);
    _pages[i].AutoScrollPosition=new Point(0,offset);
   }
  }
  private void SwitchDocument(int index){
   if(_selectedDocument>=0)_documentOffsets[_selectedDocument]=Math.Max(0,-_pages[_selectedDocument].AutoScrollPosition.Y);
   using(UiRendering.Freeze(_card)){for(int i=0;i<2;i++){_pages[i].Visible=i==index;_tabs[i].Primary=i==index;}_pages[index].BringToFront();ThemedScrollBar.RefreshFor(_pages[index]);}
   _selectedDocument=index;
   _pages[index].AutoScrollPosition=new Point(0,_documentOffsets[index]);
   ThemedScrollBar.RefreshFor(_pages[index]);
  }
  public bool PreFilterMessage(ref Message message) {
   if(message.Msg!=0x020A || !Visible || Form.ActiveForm!=this)return false;
   foreach(var page in _pages) {
    if(!page.Visible || !page.RectangleToScreen(page.ClientRectangle).Contains(Cursor.Position))continue;
    int range=Math.Max(0,page.VerticalScroll.Maximum-page.VerticalScroll.LargeChange+1);
    if(range==0)return false;
    int delta=unchecked((short)(((long)message.WParam>>16)&0xffff));
    int notches=delta/120;if(notches==0)notches=Math.Sign(delta);
    int lines=SystemInformation.MouseWheelScrollLines;
    int step=lines<0?page.ClientSize.Height:Math.Max(1,lines)*_documents[0].Font.Height;
    page.AutoScrollPosition=new Point(0,Math.Max(0,Math.Min(range,-page.AutoScrollPosition.Y-notches*step)));
    ThemedScrollBar.RefreshFor(page);return true;
   }
   return false;
  }
  protected override void Dispose(bool disposing){if(disposing)Application.RemoveMessageFilter(this);base.Dispose(disposing);}
  public void ApplyCurrentTheme(){var t=ThemeColors.Current;_card.FillColor=t.CardBackground;_card.BorderColor=t.CardBorder;_heading.ForeColor=t.TextPrimary;_summary.ForeColor=t.TextSecondary;for(int i=0;i<2;i++){_pages[i].BackColor=t.CardBackground;_documents[i].BackColor=t.CardBackground;_documents[i].ForeColor=t.TextPrimary;}Invalidate(true);}
 }
}
