using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MissionMars.MarsWorld;

namespace MissionMars
{
    // State of one offline expedition. This object stays active when rooms and the surface are exchanged.
    public sealed class BaseExpedition : MonoBehaviour
    {
        public static BaseExpedition I;
        public bool CabinReady => installed && connected && oxygen >= 99;
        public float Charge { get; private set; }
        public float NightLoad;
        Renderer[] solarSurfaces;
        Interactable meterUse;
        bool recovered, inspected, clean, connected, installed, wearing = true, lights, night, seated;
        bool waterProduced, mealEaten, looseIce, litCrop, harvested, slept;
        int meltShelter, meltGarden, waterTank, irrigation, growth, oven;
        float oxygen;
        readonly string[] items = {"Digging tool","Ice core","Water canister","Raw potatoes","Cooked meal"};
        readonly int[] bag = new int[5], luggage = new int[5], cargo = new int[5];
        const int Tool=0, Ice=1, Water=2, Raw=3, Meal=4;
        Interactable seam, field, gardenPump, cooker, luggageUse, seat;
        GameObject specimen, crust, luggageModel;
        int cuts;
        ParticleSystem coupling;
        TextMeshPro powerMeter, airMeter, shelterGauge, farmGauge, wallReadout;
        Renderer wallImage;
        int photo;
        RectTransform panel;
        GameObject overlay;
        int menuFrame;
        Waypoint waypoint;
        string lastTask;
        Transform nextTarget;
        Vector3 standPosition;
        float standYaw;
        Greenhouse farm => Greenhouse.I;
        Survival suit => Game.Suit;
        PlayerController player => Game.Player;

        public void Begin()
        {
            I=this;
            suit.Active=true; suit.O2=100; suit.Water=80; suit.Health=100;
            suit.O2Drain=.065f; suit.WaterDrain=.018f;
            SetPower(0);
            BuildMenu();
            SetUpField();
            SetUpShelter();
            SetUpResources();
            SetUpLivingQuarters();
            SetUpDisplay();
            UpdateMeters();
            Guide();
        }

        void Update()
        {
            if (!I || Game.Paused) return;
            float dt=Time.deltaTime;
            if (connected && !night && Game.Sky.Day>.05f) Charge=Mathf.Min(40,Charge+dt*.75f);
            if (installed && connected) oxygen=Mathf.Min(100,oxygen+dt*14);
            if (Time.frameCount%12==0) { UpdateMeters(); Guide(); }
            if (overlay.activeSelf)
            {
                if (Time.frameCount>menuFrame && Input.GetKeyDown(KeyCode.Escape)) ClosePanel();
            }
            else if (!UIState.Modal && !player.Frozen && !UIState.Cinematic && recovered && Input.GetKeyDown(KeyCode.B)) Storage(luggage,"Expedition case");
            if (seated && !UIState.Modal && Time.frameCount>menuFrame && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E)))
                Stand();
        }

        void Guide()
        {
            string task; Transform target;
            if (!recovered) {task="Recover the expedition case from the lander's open cargo ramp."; target=luggageUse.transform;}
            else if (!clean) {task="Inspect the solar output and clean the dusty array.";target=field.transform;}
            else if (!connected) {task="Connect the battery at the shelter power corner.";target=suit.Inside?BatteryPlug.transform:BunkerDoor.transform;}
            else if (!installed) {task="Seal the leaking supply coupling by plugging in MOXIE.";target=suit.Inside?MoxieSlot.transform:BunkerDoor.transform;}
            else if (!waterProduced && bag[Ice]==0 && meltShelter==0 && waterTank==0) {task="Take the tool from your case (B). Mine ice at Survey Site A and collect the loosened cores.";target=seam.transform;}
            else if (!waterProduced) {task="Feed ice into the shelter purifier. Fill a canister when the cycle ends.";target=suit.Inside?Tap.transform:BunkerDoor.transform;}
            else if (!harvested && bag[Ice]==0 && growth<4 && !litCrop && meltGarden==0 && irrigation==0) {task="Mine more ice for the greenhouse, or unload the cores from the rover.";target=seam.transform;}
            else if (!harvested) {task="Bring two ice cores to the greenhouse. Turn on the lights, melt the ice, then irrigate the beds.";target=farm.Room.activeSelf?gardenPump.transform:GreenhouseDoor.transform;}
            else if (!mealEaten) {task="Carry the raw potatoes to the habitat galley. Cook, collect and eat your meal.";target=HabitatRoom.Room.activeSelf?cooker.transform:HabitatDoor.transform;}
            else if (!slept) {task="Reserve overnight power at the solar array. Use a bunk to sleep until morning.";target=NightLoad==0?field.transform:HabitatRoom.Room.activeSelf?Bunk.transform:Interior.activeSelf?Bed.transform:HabitatDoor.transform;}
            else {task="Base supplied. Use B for your case, visit the weather lab, or drive another ice run.";target=null;}
            if (target && !target.gameObject.activeInHierarchy && suit.Inside)
                target=Interior.activeSelf?ExitDoor.transform:farm.Room.activeSelf?farm.ExitDoor.transform:HabitatRoom.Room.activeSelf?HabitatRoom.ExitPanel:WeatherRoom.ExitPanel;
            if (lastTask==task && nextTarget==target && Game.Hud.LastBody==task) return;
            if (waypoint!=null) Game.Hud.RemoveWaypoint(waypoint);
            lastTask=task; nextTarget=target;
            Game.Hud.Objective("BASE EXPEDITION",task);
            waypoint=target?Game.Hud.AddWaypoint("MISSION",()=>target.position+Vector3.up,HC.Cyan):null;
        }

        void Tell(string text) => Game.Hud.Say(text,3.5f);
        static void Bind(Interactable use,string verb,string label,Action action,float hold=0)
        {
            use.enabled=true; use.Verb=verb; use.Label=label; use.HoldTime=hold; use.OnUse=action;
        }
        static TextMeshPro Sign(Transform parent,Vector3 pos,string text,float yaw,float width=1.9f)
        {
            var sign=World3DText(parent,pos,text,1.5f,new Color(.73f,.96f,1));
            sign.rectTransform.sizeDelta=new Vector2(width,1.1f);
            sign.transform.localRotation=Quaternion.Euler(0,yaw,0);
            return sign;
        }
        static GameObject Fitting(string name,Transform parent,Vector3 pos,float size,float yaw)
        {
            var prefab=R.Model("P_"+name);
            if (!prefab) return null;
            var prop=Instantiate(prefab);
            var b=Util.Bounds(prop); prop.transform.localScale*=size/Mathf.Max(.01f,Mathf.Max(b.size.x,Mathf.Max(b.size.y,b.size.z)));
            b=Util.Bounds(prop); var pivot=new GameObject(name).transform;
            prop.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);
            prop.transform.SetParent(pivot,true); pivot.SetParent(parent,false); pivot.localPosition=pos; pivot.localRotation=Quaternion.Euler(0,yaw,0);
            return pivot.gameObject;
        }
        static void Hose(Transform t,Vector3 a,Vector3 b)
        {
            var pipe=ProcMesh.Prim(PrimitiveType.Cylinder,"Supply line",t,(a+b)/2,new Vector3(.055f,(a-b).magnitude/2,.055f),R.HullDark,null,false);
            pipe.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        }

        void SetUpField()
        {
            field=SolarPanel.GetComponent<Interactable>();
            Bind(field,"Inspect","Solar array output",Solar);
            solarSurfaces=SolarPanel.GetComponentsInChildren<Renderer>();
            var dust=new MaterialPropertyBlock();
            dust.SetColor("baseColorFactor",new Color(.55f,.34f,.2f));
            dust.SetColor("_BaseColor",new Color(.55f,.34f,.2f));
            foreach(var surface in solarSurfaces) surface.SetPropertyBlock(dust);
            var post=new GameObject("Array meter").transform; post.SetParent(Root.transform,false);
            post.position=G(SolarPanel.position.x,SolarPanel.position.z+2.8f);
            ProcMesh.Prim(PrimitiveType.Cube,"Meter post",post,new Vector3(0,.8f,0),new Vector3(.12f,1.6f,.12f),R.HullDark);
            var face=ProcMesh.Prim(PrimitiveType.Cube,"Power meter",post,new Vector3(0,1.6f,0),new Vector3(1.5f,.62f,.12f),R.Panel);
            powerMeter=Sign(post,new Vector3(0,1.61f,.075f),"",180);
            meterUse=Interactable.Add(face,"Inspect","Photovoltaic output",Solar);
            Computers.PowerPlanOk+=Reserve;
        }
        void OnDestroy(){Computers.PowerPlanOk-=Reserve; if(I==this)I=null;}
        void Reserve(){NightLoad=Computers.I.NightDemand;}
        void Solar()
        {
            if (!inspected){inspected=true;Bind(meterUse,"Hold E to clear dust","Solar cells",Solar,1.4f);Bind(field,"Hold E to clear dust","Solar cells",Solar,1.4f);Tell("Dust covers the cells. Hold E at the panel to clean it.");return;}
            if (!clean){clean=true;foreach(var surface in solarSurfaces) surface.SetPropertyBlock(null);Bind(meterUse,"Inspect","Stored energy",Solar);Bind(field,"Plan","Solar power",Solar);Tell("Array clear. Connect the shelter battery to collect solar power.");return;}
            if (!connected){Tell("Connect the battery drawers in the shelter.");return;}
            Computers.I.OpenPower();
        }
        void SetUpShelter()
        {
            var t=Interior.transform;
            Fitting("CRM_Reactor_FilterUnit",t,new Vector3(3.6f,0,2.95f),1.6f,180);
            Fitting("CRM_Reactor_HoseReel",t,new Vector3(-4.2f,0,3.7f),.85f,180);
            Fitting("CRM_Reactor_UtilityCabinet_02",t,new Vector3(4.5f,0,-3.56f),1.6f,270);
            var module=Fitting("CRM_Reactor_PowerModule",t,new Vector3(-1.45f,0,3.9f),1.6f,180);
            PowerSwitch.gameObject.SetActive(!module);
            if(module) Interactable.Add(module,"Inspect","Power distribution",()=>Tell($"Battery {Charge:0}/40 kWh. Set the night reserve at the solar field."));
            Hose(t,new Vector3(-3.4f,.18f,3.8f),new Vector3(-1.5f,.18f,3.8f));
            Hose(t,new Vector3(3.65f,.22f,2.92f),new Vector3(4.3f,.22f,1.67f));
            Bind(BatteryPlug,"Connect","Array battery",()=>{
                if(!clean){Tell("Inspect and clear the solar field first.");return;}
                connected=true; Cable.SetActive(true); SetPower(1);
                Bind(BatteryPlug,"Inspect","Battery",()=>Tell($"Solar battery {Charge:0.0}/40 kWh. Night reserve {NightLoad:0} kWh."));
                Tell("Solar bus connected. Find the white plume beside MOXIE.");
            });
            airMeter=Sign(t,new Vector3(4.12f,1.92f,1.57f),"",90);
            shelterGauge=Sign(t,new Vector3(4.12f,1.86f,-1.57f),"",90);
            coupling=FX.Mist(t.TransformPoint(new Vector3(4.14f,1.16f,1.57f)),t,Color.white);
            var emission=coupling.emission; emission.rateOverTime=15; coupling.Play();
            Bind(MoxieSlot,"Plug in","MOXIE supply coupling",()=>{
                if(!connected){Tell("Connect solar power to the shelter first.");return;}
                installed=true; suit.MoxieOn=true; coupling.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                MoxieInstalled.SetActive(true); MoxieBubbles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); MoxieGhost.SetActive(false);
                Tell("Coupling sealed. Keep your helmet on while the oxygen gauge rises.");
            });
            WaterGhost.SetActive(false); WaterInstalled.SetActive(true);
            Bind(Tap,"Use","Ice purifier",Purify);
            Bind(Bed,"Lie down","Shelter bunk",()=>StartCoroutine(Rest(Bed)));
            Bind(DataConsole,"Inspect","Life support",()=>Tell($"Cabin oxygen {oxygen:0}% of target. Water tank {waterTank} L. Battery {Charge:0} kWh."));
        }
        void SetUpResources()
        {
            var site=new GameObject("Survey site A").transform; site.SetParent(Root.transform,false); site.position=G(IceXZ[0]);
            var rock=R.Lit(new Color(.42f,.23f,.16f),.12f); var ice=R.Lit(new Color(.48f,.70f,.73f),.7f);
            for(int i=0;i<5;i++){
                var p=new Vector3(i*.78f-1.6f,.12f+i*.025f,.13f*Mathf.Sin(i*2));
                ProcMesh.Prim(PrimitiveType.Sphere,"Regolith bank",site,p,new Vector3(1.8f,1.04f,1.5f),rock);
                ProcMesh.Prim(PrimitiveType.Sphere,"Exposed ice stratum",site,p+new Vector3(0,.15f,-.42f),new Vector3(1.5f,.25f,1.1f),ice);
            }
            crust=ProcMesh.Prim(PrimitiveType.Sphere,"Compacted seam face",site,new Vector3(0,.33f,-.86f),new Vector3(.82f,.47f,.37f),rock);
            var stake=ProcMesh.Prim(PrimitiveType.Cube,"Survey stake",site,new Vector3(-2.5f,.7f,-.5f),new Vector3(.09f,1.4f,.09f),R.Orange);
            Sign(stake.transform.parent,new Vector3(-2.5f,1.65f,-.5f),"A · SHALLOW ICE",0);
            seam=Interactable.Add(site.gameObject,"Hold E to dig","Survey A · ice seam",Mine,1.7f,false);
            specimen=ProcMesh.Prim(PrimitiveType.Capsule,"Loosened ice core",site,new Vector3(0,.27f,-1.4f),new Vector3(.36f,.49f,.36f),ice,new Vector3(0,0,70));
            Interactable.Add(specimen,"Collect","Two ice cores",Collect); specimen.SetActive(false);
            var location=G(CrashXZ)+new Vector3(.48f,.27f,-4.1f);
            luggageModel=Util.PlaceModel("Crew_Lock_Bag",location,.78f,30,Wreck);
            luggageUse=Interactable.Add(luggageModel,"Recover","Expedition tool case",Recover);
            var rack=ProcMesh.Prim(PrimitiveType.Cube,"Cargo latch",TheRover.transform,new Vector3(0,1.28f,-2.45f),new Vector3(.75f,.35f,.18f),R.Orange);
            Interactable.Add(rack,"Transfer","Rover cargo",()=>Storage(cargo,"Rover cargo"));
            gardenPump=ModelRoom.Part(farm.Room,"Ice melt system").GetComponent<Interactable>();
            farmGauge=Sign(farm.Room.transform,new Vector3(4.85f,1.5f,-1.55f),"",180);
            Bind(gardenPump,"Use","Greenhouse ice inlet",Irrigate);
            Bind(farm.Room.transform.Find("LED switch").GetComponent<Interactable>(),"Switch","Grow lamps",()=>{
                if(!connected){Tell("Connect the solar battery first.");return;}
                lights=true;farm.SetExpeditionCrop(growth==4,true);Tell("Grow lamps on. Load ice at the melt system.");
            });
            foreach(var bed in farm.Room.GetComponentsInChildren<Interactable>(true))
                if(bed.Label=="potatoes") Bind(bed,"Inspect or harvest","Potato bed",Harvest);
        }
        void Recover(){recovered=true;luggage[Tool]=1;luggageModel.SetActive(false);Storage(luggage,"Recovered case · take the digging tool");}
        void Mine(){
            if(bag[Tool]==0){Tell("Recover the cargo case, then press B and take the digging tool.");return;}
            if(looseIce){Tell("Collect the loose ice beside the seam first.");return;}
            looseIce=true;crust.SetActive(false);specimen.transform.localPosition=new Vector3(crust.transform.localPosition.x,.27f,-1.4f);specimen.SetActive(true);Sfx.Play("thud");Tell("Two cores are loose. Aim at them and press E to collect.");
        }
        void Collect(){if(!looseIce)return;bag[Ice]+=2;looseIce=false;specimen.SetActive(false);cuts++;crust.transform.localPosition=new Vector3(Mathf.Sin(cuts*2f)*1.4f,.33f,-.86f);crust.SetActive(true);Tell($"Packed two ice cores. Bag: {bag[Ice]}. Mine more or load the rover's rear cargo latch.");}
        void Purify()
        {
            if(!connected){Tell("Purifier needs solar power.");return;}
            if(waterTank>0){waterTank--;bag[Water]++;waterProduced=true;Tell("One litre bottled. Press B to drink or store the canister.");return;}
            if(meltShelter>0){Tell("Filtering ice. Watch the tank gauge.");return;}
            if(bag[Ice]==0){Tell("Bring an ice core from Survey A in your bag.");return;}
            bag[Ice]--;meltShelter=1;StartCoroutine(Melt(false));
        }
        IEnumerator Melt(bool greenhouse){
            Tell("Ice loaded. Melting and filtering…");yield return new WaitForSeconds(4);
            if(greenhouse){meltGarden=0;irrigation+=2;}else{meltShelter=0;waterTank+=2;}
            Tell(greenhouse?"Irrigation tank +2 L. Use the pump to start irrigation.":"Purifier ready. Use the outlet to fill a canister.");
        }
        void Irrigate(){
            if(!connected){Tell("Connect solar power first.");return;}
            if(meltGarden>0){Tell("Melting ice. Check the tank gauge.");return;}
            if(irrigation>0 && lights && !litCrop && growth<4){litCrop=true;StartCoroutine(Grow());return;}
            if(bag[Ice]==0){Tell($"Irrigation {irrigation} L. Bring ice cores and turn on the grow lights.");return;}
            bag[Ice]--;meltGarden=1;StartCoroutine(Melt(true));
        }
        IEnumerator Grow(){
            while(growth<4 && irrigation>0){irrigation--;yield return new WaitForSeconds(2);growth++;}
            litCrop=false;
            if(growth==4){farm.SetExpeditionCrop(true,true);Tell("Potatoes ready. Press E on a planting bed.");}
            else Tell("Growth paused. Melt another core, then use the irrigation pump.");
        }
        void Harvest(){
            if(growth!=4){Tell($"Growth {growth*25}%. Irrigation tank {irrigation} L.");return;}
            bag[Raw]+=2;growth=0;harvested=true;farm.SetExpeditionCrop(false,lights);Tell("Two raw potatoes packed. Use the cooker in the habitat galley.");
        }
        void SetUpLivingQuarters(){
            cooker=Kitchen;Bind(cooker,"Use","Galley cooker",Cook);
            Bind(Bunk,"Lie down","Habitat bunk",()=>StartCoroutine(Rest(Bunk)));
            Bind(Locker,"Inspect","EVA locker",LockerMenu);
            var desk=ModelRoom.Part(HabitatRoom.Room,"Crew workstation");
            var chair=ProcMesh.Prim(PrimitiveType.Cube,"Operator chair",HabitatRoom.Room.transform,new Vector3(desk.localPosition.x,.46f,.68f),new Vector3(.45f,.10f,.44f),R.Panel);
            ProcMesh.Prim(PrimitiveType.Cylinder,"Seat post",HabitatRoom.Room.transform,new Vector3(desk.localPosition.x,.22f,.68f),new Vector3(.17f,.22f,.17f),R.HullDark);
            seat=Interactable.Add(chair,"Sit","Navigation chair",()=>Sit(chair.transform.position,Computers.I.OpenOrbit));
            var mess=ModelRoom.Part(HabitatRoom.Room,"Mess table");Interactable.Add(mess.gameObject,"Sit","Galley chair",()=>Sit(new Vector3(mess.position.x,.46f+HabitatRoom.Room.transform.position.y,mess.position.z-.65f),null));
        }
        void Cook(){
            if(!connected){Tell("The galley needs power.");return;}
            if(oven==1){Tell("Cooking. The warmer will chime when ready.");return;}
            if(oven==2){oven=0;bag[Meal]++;Tell("Meal collected. Press B and choose Eat cooked meal.");return;}
            if(bag[Raw]==0){Tell("Harvest potatoes from a greenhouse bed first.");return;}
            bag[Raw]--;oven=1;StartCoroutine(CookCycle());
        }
        IEnumerator CookCycle(){Tell("Galley warmer loaded. Cooking…");yield return new WaitForSeconds(4);oven=2;Sfx.Play("beep");Tell("Meal ready to collect.");}
        void Sit(Vector3 cushion,Action<Action> computer){
            if(player.Frozen)return;
            standPosition=player.transform.position;standYaw=player.YawDeg;seated=true;player.Frozen=true;
            player.Teleport(cushion,0);player.Resting=true;player.HeadHeight=.82f;
            Tell("Seated at the workstation. Space to stand.");menuFrame=Time.frameCount;
            if(computer!=null) computer(()=>{menuFrame=Time.frameCount;Tell("Press Space or E to stand.");});
            else Tell("Seated at the mess table. Press Space or E to stand.");
        }
        void Stand(){seated=false;player.Resting=false;player.HeadHeight=1.62f;player.Teleport(standPosition,standYaw);player.Frozen=false;}
        IEnumerator Rest(Interactable bed){
            if(player.Frozen)yield break;
            if(!CabinReady){Tell("The cabin needs working oxygen before you can sleep.");yield break;}
            if(NightLoad<=0 || Charge<NightLoad){Tell($"Plan night power at the solar field. Battery {Charge:0.0} / 40 kWh.");yield break;}
            var from=player.transform.position;float yaw=player.YawDeg;
            player.Frozen=true;player.Resting=true;night=true;
            var pillow=bed.transform.position+bed.transform.right*.62f;
            player.Teleport(new Vector3(pillow.x,bed.transform.position.y-.35f,pillow.z),bed.transform.eulerAngles.y+90);
            player.HeadHeight=.24f;player.ExtraRoll=70;yield return new WaitForSeconds(.8f);
            yield return Cinema.I.ToBlack(.7f);
            Game.Sky.TimeOfDay=.9f;Game.Sky.Apply();float start=Charge;
            for(float t=0;t<1;t+=Time.deltaTime/2){Charge=Mathf.Lerp(start,start-NightLoad,t);yield return null;}
            Charge=Mathf.Max(0,start-NightLoad);NightLoad=0;Game.Story.Sol++;slept=true;
            Game.Sky.TimeOfDay=.32f;Game.Sky.Apply();suit.Health=Mathf.Min(100,suit.Health+20);
            yield return Cinema.I.FromBlack(.7f);yield return new WaitForSeconds(.5f);
            player.Resting=false;player.HeadHeight=1.62f;player.ExtraRoll=0;player.Teleport(from,yaw);player.Frozen=false;night=false;
            Tell($"Morning, sol {Game.Story.Sol}. Overnight battery use {start-Charge:0} kWh.");
        }
        public bool CanExit(){
            if(wearing)return true;
            Tell("Equip your EVA suit at the habitat locker before cycling the airlock.");return false;
        }
        void LockerMenu(){
            Menu("EVA locker",$"Suit oxygen {suit.O2:0}%   ·   Pressure seal {(wearing?"locked":"open")}   ·   Cabin supply {(CabinReady?"ready":"unavailable")}");
            Button(wearing?"Remove EVA suit":"Equip and seal suit",()=>{
                if(wearing&&!CabinReady){Tell("Keep the suit sealed until cabin oxygen is ready.");ClosePanel();return;}
                wearing=!wearing;LockerMenu();
            });
            Button("Refill oxygen cylinder",()=>{if(CabinReady)suit.O2=100;else Tell("MOXIE must reach operating pressure first.");LockerMenu();});
        }
        void UpdateMeters(){
            powerMeter.text=$"{(clean?4.8:0.6)*Mathf.Clamp01(Game.Sky.Day):0.0} kW   /   {Charge:0} kWh";
            airMeter.text=$"CABIN OXYGEN\n{oxygen:0}% OF TARGET";
            shelterGauge.text=meltShelter>0?"MELT CYCLE":"WATER   "+waterTank+" L";
            farmGauge.text=$"IRRIGATION   {irrigation} L\n{(meltGarden>0?"MELTING":litCrop?"PUMP ON":"STANDBY")}   {growth*25}%";
        }
        void BuildMenu(){
            overlay=new GameObject("Field stations",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            overlay.transform.SetParent(transform,false);
            var cv=overlay.GetComponent<Canvas>();cv.renderMode=RenderMode.ScreenSpaceOverlay;cv.sortingOrder=70;
            var scaler=overlay.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            var shade=UIX.Img(overlay.transform,"Shade",new Color(.02f,.025f,.04f,.8f),null,true);shade.rectTransform.Fill();
            panel=UIX.Img(overlay.transform,"Panel",new Color(.045f,.077f,.091f),UISprites.Round,true).rectTransform;
            panel.anchorMin=panel.anchorMax=panel.pivot=Vector2.one*.5f;panel.sizeDelta=new Vector2(1020,700);
            overlay.SetActive(false);
        }
        int options;
        void Menu(string title,string description){
            if(!overlay.activeSelf){overlay.SetActive(true);UIState.Push();}
            menuFrame=Time.frameCount; options=0;
            for(int i=panel.childCount-1;i>=0;i--){var child=panel.GetChild(i).gameObject;child.SetActive(false);Destroy(child);}
            Text(title,36,new Vector2(470,-58),new Vector2(850,55),HC.Cyan);
            Text(description,24,new Vector2(510,-142),new Vector2(920,92),HC.Text);
            var close=UIX.Button(panel,"Close",ClosePanel,120,50,20,HC.Cyan);
            Position((RectTransform)close.transform,935,-49,120,50);
        }
        static void Position(RectTransform rt,float x,float y,float w,float h){rt.anchorMin=rt.anchorMax=Vector2.up;rt.pivot=Vector2.one*.5f;rt.anchoredPosition=new Vector2(x,y);rt.sizeDelta=new Vector2(w,h);}
        void Text(string value,int size,Vector2 pos,Vector2 dims,Color color){
            var t=UIX.Text(panel,value,size,color,R.KidBody);Position(t.rectTransform,pos.x,pos.y,dims.x,dims.y);
        }
        void Button(string label,Action click,bool enabled=true){
            var b=UIX.Button(panel,label,click,440,52,22,HC.Cyan);
            Position((RectTransform)b.transform,options%2==0?269:751,-217-(options/2)*66,440,52);options++;b.interactable=enabled;
        }
        void ClosePanel(){if(!overlay.activeSelf)return;overlay.SetActive(false);UIState.Pop();Computers.ClosedFrame=Time.frameCount;}
        void Storage(int[] bin,string name){
            Menu(name,$"Hydration {suit.Water:0}%   |   Health {suit.Health:0}%   |   Suit oxygen {suit.O2:0}%\nTransfer one item per click. Press B to use your case away from the rover.");
            for(int i=0;i<items.Length;i++){int k=i;
                Button($"Stow {items[k]} ({bag[k]})",()=>{if(bag[k]>0){bag[k]--;bin[k]++;}Storage(bin,name);},bag[k]>0);
                Button($"Take {items[k]} ({bin[k]})",()=>{if(bin[k]>0){bin[k]--;bag[k]++;}Storage(bin,name);},bin[k]>0);
            }
            Button("Drink one canister",()=>{if(bag[Water]>0){bag[Water]--;suit.Water=Mathf.Min(100,suit.Water+25);}Storage(bin,name);},bag[Water]>0);
            Button("Eat cooked meal",()=>{if(bag[Meal]>0){bag[Meal]--;suit.Health=Mathf.Min(100,suit.Health+20);mealEaten=true;}Storage(bin,name);},bag[Meal]>0);
        }
        void SetUpDisplay(){
            var room=WeatherRoom.Room.transform;
            var stool=ProcMesh.Prim(PrimitiveType.Cylinder,"Forecast stool",room,new Vector3(.25f,.45f,-.28f),new Vector3(.49f,.055f,.49f),R.Panel);
            ProcMesh.Prim(PrimitiveType.Cylinder,"Forecast stool base",room,new Vector3(.25f,.2f,-.28f),new Vector3(.14f,.2f,.14f),R.HullDark);
            Interactable.Add(stool,"Sit","Weather workstation",()=>Sit(stool.transform.position,Computers.I.OpenWeather));
            var screen=ProcMesh.Prim(PrimitiveType.Cube,"Weather station widescreen",room,new Vector3(-.32f,1.7f,-1.84f),new Vector3(2.48f,1.16f,.055f),R.Panel);
            var image=GameObject.CreatePrimitive(PrimitiveType.Quad);
            image.transform.SetParent(room,false);image.transform.localPosition=new Vector3(-.32f,1.72f,-1.78f);image.transform.localRotation=Quaternion.Euler(0,180,0);image.transform.localScale=new Vector3(2.4f,1.08f,1);
            Destroy(image.GetComponent<Collider>());wallImage=image.GetComponent<Renderer>();
            wallImage.sharedMaterial=R.Emissive(Color.white,Color.white);
            wallReadout=Sign(room,new Vector3(-.32f,1.74f,-1.73f),"",180,2.32f);
            Interactable.Add(screen,"Select view","Weather lab display",Display);
            ShowPhoto();
        }
        void ShowPhoto(){
            var tex=NasaLive.I.Photos[photo];wallImage.enabled=tex;
            if(tex){var mat=wallImage.material;mat.mainTexture=tex;mat.SetTexture("_EmissionMap",tex);}
            wallReadout.transform.localPosition=new Vector3(-.32f,1.07f,-1.72f);
            wallReadout.text=NasaLive.I.PhotoTitles[photo];wallReadout.fontSize=1.15f;
        }
        void ShowReadings(){
            wallImage.enabled=false;
            wallReadout.transform.localPosition=new Vector3(-.32f,1.72f,-1.72f);wallReadout.fontSize=1.55f;
            var sol=NasaApi.I.LastSol;var n=NasaLive.I;
            wallReadout.text=$"INSIGHT   ·   {sol.date}\n{sol.tempMinC:0} to {sol.tempMaxC:0} °C   |   {sol.pressurePa:0} Pa\nWind {sol.windMs:0.0} m/s {sol.windDir}\nEARTH   {n.DistanceKm:N0} km\nSignal   {n.DelayText} one way";
        }
        void Display(){
            Menu("Weather lab display","Choose what appears on the wall. Mars weather is an archived InSight observation. Earth distance comes from the bundled JPL table.");
            Button("NASA photo gallery",()=>{ShowPhoto();ClosePanel();});
            Button("Station statistics",()=>{ShowReadings();ClosePanel();});
            Button("Next NASA photograph",()=>{photo=(photo+1)%NasaLive.I.Photos.Length;ShowPhoto();ClosePanel();});
        }
    }
}
