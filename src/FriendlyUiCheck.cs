using System;
using System.IO;
using System.Reflection;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
class FriendlyUiCheck
{
 static BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Get(object obj,string name){return obj.GetType().GetField(name,Private|BindingFlags.Public).GetValue(obj);}
 static void Set(object obj,string name,object value){obj.GetType().GetField(name,Private|BindingFlags.Public).SetValue(obj,value);}
 static object Invoke(object obj,string name,params object[] args){return obj.GetType().GetMethod(name,Private|BindingFlags.Public).Invoke(obj,args);}
 static void Assert(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
 static object Copy(object source){object c=Activator.CreateInstance(source.GetType(),true);foreach(FieldInfo f in source.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public))f.SetValue(c,f.GetValue(source));return c;}
 static void Snapshot(Form form,object result,string state){Set(result,"State",Enum.Parse(Get(result,"State").GetType(),state));Set(form,"lastInspection",result);Invoke(form,"ApplyInspection",result);Application.DoEvents();}
 static void Render(Form f,string file){using(Bitmap b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(file,ImageFormat.Png);}}
 [STAThread] static int Main(string[] args)
 {
  try {
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   Assembly a=Assembly.LoadFile(Path.GetFullPath(args[0]));
   a.GetType("WeixinAntiRevoke.EmbeddedDependencies").GetMethod("Initialize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
   Directory.CreateDirectory(args[1]);
   using(Form form=(Form)Activator.CreateInstance(a.GetType("WeixinAntiRevoke.MainForm"),true)){
    form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-32000,-32000);form.ShowInTaskbar=false;form.Show();Application.DoEvents();
    DateTime deadline=DateTime.UtcNow.AddSeconds(60);
    while((bool)Get(form,"operationBusy")&&DateTime.UtcNow<deadline){Application.DoEvents();System.Threading.Thread.Sleep(20);}
    Assert(!(bool)Get(form,"operationBusy"),"background check completes");
    object actual=Get(form,"lastInspection");Assert(actual!=null,"real inspection result received");
    Console.WriteLine("ACTUAL="+Get(actual,"State"));
    Assert(Get(actual,"State").ToString()=="Ready"||Get(actual,"State").ToString()=="PromptPatched"||Get(actual,"State").ToString()=="LegacyPatched","current Weixin recognized");
    Render(form,Path.Combine(args[1],"preview.png"));
    object release=Activator.CreateInstance(a.GetType("WeixinAntiRevoke.ReleaseManifest"),true);
    Set(release,"Version","3.2.1");Set(release,"Notes","改善新版微信的使用体验。");
    Set(form,"pendingRelease",release);Invoke(form,"RefreshUpdateButton");
    Button update=(Button)Get(form,"updateButton");
    Assert(update.Enabled&&update.Text=="发现新版 · 点击更新","new release has a clear update action");
    Render(form,Path.Combine(args[1],"update-available.png"));
    Invoke(form,"SetBusy",true);Assert(!update.Enabled,"update cannot start during a Weixin operation");
    Invoke(form,"SetBusy",false);Set(form,"pendingRelease",null);Invoke(form,"RefreshUpdateButton");
    Button install=(Button)Get(form,"installButton"),restore=(Button)Get(form,"restoreButton");
    object ready=Copy(actual);Snapshot(form,ready,"Ready");
    Assert(install.Enabled&&!restore.Enabled&&install.Text=="开启防撤回","ready: one clear enable action");
    Render(form,Path.Combine(args[1],"ready.png"));
    Invoke(form,"SetBusy",true);
    Assert(!install.Enabled&&!restore.Enabled&&!((Button)Get(form,"promptCard")).Enabled&&!((Button)Get(form,"autoCheckButton")).Enabled,"busy: actions locked");
    Invoke(form,"SetBusy",false);Assert(install.Enabled,"busy completion restores controls");
    object active=Copy(actual);Snapshot(form,active,"PromptPatched");Invoke(form,"SetSelection",true);
    Assert(!install.Enabled&&restore.Enabled,"active same mode: no duplicate enable");
    ((Button)Get(form,"quietCard")).PerformClick();
    Assert(install.Enabled&&install.Text=="切换到这个效果"&&(bool)Get(form,"promptMode")==false,"effect card switches selected mode");
    object unsupported=Copy(actual);Snapshot(form,unsupported,"Unsupported");
    Assert(!install.Enabled&&!restore.Enabled,"unsupported cannot enable or disable");
    Assert(!((Label)Get(form,"stateValue")).Text.Contains("结构"),"unsupported copy stays understandable");
    Render(form,Path.Combine(args[1],"unsupported.png"));
    object missing=Copy(actual);Snapshot(form,missing,"Missing");Assert(((Label)Get(form,"stateValue")).Text.Contains("还没找到"),"missing Weixin has actionable copy");
    object limited=Copy(actual);object plan=Copy(Get(actual,"Adaptive"));Set(plan,"PromptEdits",null);Set(limited,"Adaptive",plan);Snapshot(form,limited,"Ready");
    Assert(!(bool)Get(form,"promptMode")&&!((Button)Get(form,"promptCard")).Enabled&&install.Enabled,"unavailable effect selects working option");
    Snapshot(form,ready,"Ready");Invoke(form,"SetSelection",true);
    form.ClientSize=new Size(1100,760);Application.DoEvents();
    Assert(((Control)Get(form,"quietCard")).Right<form.ClientSize.Width&&((Control)Get(form,"helpButton")).Bottom<=form.ClientSize.Height,"resized layout remains inside window");
    foreach(Control c in form.Controls)if(c is Label)Assert(!c.Text.Contains("SHA-")&&!c.Text.Contains("DLL")&&!c.Text.Contains("补丁")&&!c.Text.Contains("哈希"),"no jargon: "+c.Name);
    bool embedded=false;foreach(Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())if(loaded.GetName().Name=="Iced"&&loaded.Location=="")embedded=true;Assert(embedded,"standalone EXE uses embedded decoder");
    form.Close();
   }
   Console.WriteLine("ALL UI CHECKS PASS; no patch operation invoked");return 0;
  }catch(Exception e){Console.Error.WriteLine(e.Message);if(e.InnerException!=null)Console.Error.WriteLine(e.InnerException.Message);return 1;}
 }
}
