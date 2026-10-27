using WhaleCompanion;
static class TenderizeChecks
{
 public static void Run(Action<bool,string> check)
 {
  const ulong monster=0x100000;var m=new MemoryFixture();ulong at=monster+0x1C458;
  m.Values[at+48]=-1;m.Float(at+8,0);m.Float(at+12,174);
  var tracker=new TenderizeTracker();check(tracker.Observe(TenderizeProbe.Read(m,monster,"421810",0),0).Length==0,"inactive native tenderize slot establishes silent baseline");
  check(TenderizeProbe.Read(m,monster,"unknown",0).Length==0,"unsupported revision cannot read tenderize");
  m.Values[at+48]=4;m.Float(at+8,.1f);
  check(tracker.Observe(TenderizeProbe.Read(m,monster,"421810",.25),.25).Length==0,"active part identity waits for stable second sample");
  m.Float(at+8,.35f);check(tracker.Observe(TenderizeProbe.Read(m,monster,"421810",.5),.5) is [{Part:4}],"native part activation confirms softening result despite elapsed timer increasing");
  m.Float(at+8,.6f);check(tracker.Observe(TenderizeProbe.Read(m,monster,"421810",.75),.75).Length==0,"elapsed timer increase is not a refresh");
  m.Values[at+48]=-1;m.Float(at+8,180.01549f);
  check(TenderizeProbe.Read(m,monster,"421810",1).Length==1&&tracker.Observe(TenderizeProbe.Read(m,monster,"421810",1),1).Length==0,"expiry sentinel remains readable without being a new softening");
  tracker.Reset();m.Values[at+48]=4;m.Float(at+8,10);tracker.Observe(TenderizeProbe.Read(m,monster,"421810",0),0);
  m.Float(at+8,10.25f);check(tracker.Observe(TenderizeProbe.Read(m,monster,"421810",.25),.25).Length==0,"already soft on initial load stays silent");
  m.Float(at+8,0);tracker.Observe(TenderizeProbe.Read(m,monster,"421810",.5),.5);
  m.Float(at+8,.25f);check(tracker.Observe(TenderizeProbe.Read(m,monster,"421810",.75),.75).Length==1,"elapsed timer reset can confirm a wound refresh");
  tracker.Reset();tracker.Observe([new(monster,-1,0,0,0,0)],0);tracker.Observe([new(monster,4,0,174,.25,0)],.25);
  check(tracker.Observe([new(monster,-1,0,174,.5,0)],.5).Length==0,"one-frame active part does not confirm softening");
  tracker.Reset();tracker.Observe([new(monster,-1,0,0,0,0)],0);
  check(tracker.Observe([new(monster,4,double.NaN,174,.25,0)],.25).Length==0,"nonfinite elapsed timer rejected");
  tracker.Reset();tracker.Observe([new(monster,-1,0,0,0,0)],0);
  check(tracker.Observe([new(monster,4,0,174,.25,0),new(monster,5,0,174,.25,0)],.25).Length==0,"duplicate native slot rejected");
  tracker.Reset();tracker.Observe([new(monster,-1,0,0,0,0)],0);tracker.Observe([new(monster,4,0,174,.25,0)],.25);
  check(tracker.Observe([new(monster,4,1,174,2,0)],2).Length==0,"sampling gap does not confirm old softening candidate");
 }
}
