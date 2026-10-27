using WhaleCompanion;
static void Check(bool value, string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
var p = new CompanionEvents();
Check(p.Observe(100,100,0)==null,"baseline silent");
Check(p.Observe(20,100,1)=="low_health","low threshold crossing");
Check(p.Observe(19,100,2)==null,"low no repetition");
p.Observe(100,100,30);
Check(p.Observe(20,100,31)=="low_health","healing rearms low event");
p=new(); p.Observe(200,200,0);
Check(p.Observe(100,100,1)==null,"max HP change excluded");
p=new(); p.Observe(100,100,0); p.Observe(90,100,1); p.Observe(80,100,2);
Check(p.Observe(80,100,3)=="health_loss","damage merged over two seconds");
p.Observe(60,100,4);
Check(p.Observe(60,100,6)==null,"cooldown suppresses repeat");
p=new(); p.Observe(100,100,0);
Check(p.Observe(0,100,1)==null,"zero HP not assumed faint");
p.Reset(); Check(p.Observe(20,100,2)==null,"new scene baseline silent");
