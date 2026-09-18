using System.Collections.Generic;
using UnityEngine;

// Character-specific geometry and rig. +Z is forward, +Y is up. Every limb's long
// axis is local +Y. Geometry is built once; only joint transforms move at runtime.
public sealed class SkeletonArcherRig
{
    readonly List<Mesh> meshes = new();
    readonly List<Material> materials = new();
    readonly Material ivory, dark, red, wood, orange;
    public readonly Transform pelvis, chest, head, bow, arrow, rightHand;
    readonly Transform leftUpper, leftFore, leftHand, rightUpper, rightFore;
    readonly Transform leftLeg, leftShin, rightLeg, rightShin;
    readonly Transform stringTop, stringBottom;
    readonly Transform root;
    public Vector3 ArrowTip => arrow.TransformPoint(Vector3.forward * .88f);
    public Vector3 ArrowDirection => arrow.forward;

    public SkeletonArcherRig(Transform parent)
    {
        root = parent;
        ivory = Mat("Ivory bone", new Color(.92f,.89f,.79f));
        dark = Mat("Deep sockets", new Color(.055f,.035f,.03f));
        red = Mat("Red waist cloth and fletching", new Color(.64f,.075f,.055f));
        wood = Mat("Bow and quiver", new Color(.43f,.205f,.105f));
        orange = Mat("Ember eyes", new Color(1,.42f,.025f));
        orange.EnableKeyword("_EMISSION"); orange.SetColor("_EmissionColor", new Color(1,.23f,.015f)*1.6f);
        pelvis = Joint("Pelvis", root, new Vector3(0,.66f,0));
        var hips = new Geometry(); hips.Ellipsoid(Vector3.zero,new Vector3(.29f,.13f,.17f));
        hips.Ellipsoid(new Vector3(-.19f,-.045f,0),new Vector3(.12f,.13f,.15f));
        hips.Ellipsoid(new Vector3(.19f,-.045f,0),new Vector3(.12f,.13f,.15f));
        Part("Hip bones",pelvis,hips,ivory);
        var cloth=new Geometry();
        cloth.Ellipsoid(new Vector3(0,.03f,0),new Vector3(.30f,.085f,.19f));
        cloth.Panel(new Vector3(-.24f,.02f,.18f),new Vector3(.02f,.015f,.20f),new Vector3(-.17f,-.33f,.21f));
        cloth.Panel(new Vector3(-.035f,.015f,.20f),new Vector3(.22f,.02f,.18f),new Vector3(.11f,-.28f,.24f));
        cloth.Panel(new Vector3(-.19f,0,-.17f),new Vector3(.20f,0,-.17f),new Vector3(.04f,-.29f,-.20f));
        Part("Tattered red waist cloth",pelvis,cloth,red);
        chest = Joint("Spine / Chest",pelvis,new Vector3(0,.42f,0));
        var ribs = new Geometry(); ribs.Bone(new Vector3(0,-.42f,-.04f),new Vector3(0,.19f,-.04f),.065f);
        for(int i=0;i<3;i++)
        {
            float y=.12f-i*.17f, w=.33f-i*.06f;
            foreach(float side in new[]{-1f,1f})
            {
                Vector3 a=new(0,y,-.055f), b=new(side*w,y+.035f,.025f), c=new(side*w*.8f,y-.055f,.17f), d=new(0,y-.07f,.19f);
                ribs.Bone(a,b,.052f); ribs.Bone(b,c,.052f); ribs.Bone(c,d,.048f);
            }
        }
        ribs.Bone(new Vector3(-.31f,.15f,0),new Vector3(.31f,.15f,0),.055f);
        ribs.Bone(new Vector3(0,-.14f,.18f),new Vector3(0,.15f,.16f),.055f);
        Part("Rib cage",chest,ribs,ivory);
        var neck=Joint("Neck",chest,new Vector3(0,.23f,0));
        var neckBone=new Geometry(); neckBone.Bone(Vector3.down*.1f,Vector3.up*.13f,.067f);
        Part("Neck vertebra",neck,neckBone,ivory);
        var strap=new Geometry(); strap.Bone(new Vector3(.25f,.17f,.13f),new Vector3(-.2f,-.24f,.19f),.041f);
        Part("Diagonal quiver strap",chest,strap,wood);
        head=Joint("Oversized skull",neck,new Vector3(0,.35f,-.025f));
        // Still wider than the shoulder span, but leave the rib cage visible
        // beneath the skull's projected footprint at the actual gameplay angle.
        head.localScale=Vector3.one*.88f;
        var skull=new Geometry(); skull.ChamferedBlock(new Vector3(0,.07f,0),new Vector3(.37f,.34f,.28f));
        skull.ChamferedBlock(new Vector3(0,-.18f,.075f),new Vector3(.245f,.09f,.225f));
        for(int i=0;i<5;i++) skull.ChamferedBlock(new Vector3((i-2)*.09f,-.265f,.268f),new Vector3(.037f,.06f,.032f));
        Part("Skull and teeth",head,skull,ivory);
        var sockets=new Geometry();
        sockets.Ellipsoid(new Vector3(-.16f,.005f,.279f),new Vector3(.138f,.16f,.039f));
        sockets.Ellipsoid(new Vector3(.16f,.005f,.279f),new Vector3(.138f,.16f,.039f));
        sockets.Ellipsoid(new Vector3(0,-.16f,.298f),new Vector3(.041f,.05f,.023f),4,3);
        Part("Sockets nose and grin",head,sockets,dark);
        var eyes=new Geometry();
        eyes.Ellipsoid(new Vector3(-.16f,-.01f,.318f),new Vector3(.052f,.075f,.022f));
        eyes.Ellipsoid(new Vector3(.16f,-.01f,.318f),new Vector3(.052f,.075f,.022f));
        Part("Orange eyes",head,eyes,orange);
        MakeArm("Left",-.3f,out leftUpper,out leftFore,out leftHand);
        MakeArm("Right",.3f,out rightUpper,out rightFore,out rightHand);
        MakeLeg("Left",-.18f,out leftLeg,out leftShin);
        MakeLeg("Right",.18f,out rightLeg,out rightShin);
        var quiver=Joint("Back quiver",chest,new Vector3(.23f,.02f,-.23f));
        quiver.localRotation=Quaternion.Euler(0,0,-22);
        var leather=new Geometry(); leather.Bone(new Vector3(0,-.29f,0),new Vector3(0,.23f,0),.14f);
        Part("Quiver leather",quiver,leather,wood);
        var rim=new Geometry(); rim.Ellipsoid(new Vector3(0,.235f,0),new Vector3(.16f,.04f,.15f));
        Part("Quiver leather rim",quiver,rim,wood);
        for(int i=0;i<3;i++)
        {
            var spare=CreateArrow(quiver,"Spare arrow"); spare.localPosition=new Vector3((i-1)*.085f,.65f+Mathf.Abs(i-1)*.035f,0);
            // Store point-down: red fletching, rather than arrowheads, clears the rim.
            spare.localRotation=Quaternion.Euler(90,0,(i-1)*-8); spare.localScale=Vector3.one*.7f;
        }
        bow=Joint("Bow (+Y limbs, +Z shot)",leftHand,Vector3.zero);
        var stave=new Geometry();
        // A slight outward sweep keeps the wooden C silhouette visible head-on
        // from the elevated camera, while the string still draws along -Z.
        Vector3[] points={new(-.19f,-.67f,-.18f),new(-.13f,-.49f,-.035f),new(-.03f,-.26f,.035f),Vector3.zero,new(-.03f,.26f,.035f),new(-.13f,.49f,-.035f),new(-.19f,.67f,-.18f)};
        for(int i=0;i<points.Length-1;i++) stave.Bone(points[i],points[i+1],.055f);
        Part("Curved wooden stave",bow,stave,wood);
        stringTop=String("Upper string"); stringBottom=String("Lower string");
        arrow=CreateArrow(chest,"Nocked arrow");
        Pose(0,0,0,0,false);
    }
    Material Mat(string name,Color color)
    {
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")) {name=name,color=color};
        mat.SetFloat("_Smoothness",.05f); materials.Add(mat); return mat;
    }
    static Transform Joint(string name,Transform parent,Vector3 pos)
    {
        var t=new GameObject(name).transform; t.SetParent(parent,false); t.localPosition=pos; return t;
    }
    Transform Part(string name,Transform parent,Geometry geometry,Material material)
    {
        var t=Joint(name,parent,Vector3.zero); var mesh=geometry.Build(name); meshes.Add(mesh);
        t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material; return t;
    }
    Transform BonePart(string name,Transform parent,float length,float radius)
    {
        var g=new Geometry(); g.Bone(Vector3.zero,Vector3.up*length,radius);
        g.Ellipsoid(Vector3.zero,Vector3.one*radius*1.5f,6,4);
        return Part(name,parent,g,ivory);
    }
    void MakeArm(string side,float x,out Transform upper,out Transform fore,out Transform hand)
    {
        upper=BonePart(side+" upper arm",chest,.36f,.056f); upper.localPosition=new Vector3(x,.12f,0);
        fore=BonePart(side+" forearm",upper,.38f,.048f); fore.localPosition=Vector3.up*.36f;
        hand=Joint(side+" hand",fore,Vector3.up*.38f);
        var g=new Geometry(); g.ChamferedBlock(Vector3.zero,new Vector3(.078f,.09f,.075f)); Part("Hand",hand,g,ivory);
    }
    void MakeLeg(string side,float x,out Transform upper,out Transform shin)
    {
        upper=BonePart(side+" thigh",pelvis,.28f,.065f); upper.localPosition=new Vector3(x,-.06f,0);
        shin=BonePart(side+" shin",upper,.28f,.052f); shin.localPosition=Vector3.up*.28f;
        // Thigh/shin local +Y point down; local -Z therefore points root-forward.
        var foot=new Geometry(); foot.Ellipsoid(new Vector3(0,.28f,-.065f),new Vector3(.105f,.07f,.18f));
        Part(side+" foot",shin,foot,ivory);
    }
    Transform String(string name)
    {
        var g=new Geometry(); g.Bone(Vector3.zero,Vector3.up,.012f);
        return Part(name,bow,g,ivory);
    }
    public Transform CreateArrow(Transform parent,string name)
    {
        var t=Joint(name,parent,Vector3.zero);
        var shaft=new Geometry(); shaft.Bone(Vector3.zero,new Vector3(0,0,.77f),.028f);
        Part("Shaft",t,shaft,wood);
        var details=new Geometry(); details.Ellipsoid(new Vector3(0,0,.8f),new Vector3(.067f,.038f,.10f),4,2);
        Part("Arrowhead",t,details,ivory);
        var feathers=new Geometry();
        feathers.ChamferedBlock(new Vector3(0,0,.10f),new Vector3(.105f,.018f,.14f));
        feathers.ChamferedBlock(new Vector3(0,0,.10f),new Vector3(.018f,.105f,.14f));
        Part("Red fletching",t,feathers,red); return t;
    }
    // Analytic two-segment solve in chest space. A stable pole chooses the elbow
    // side explicitly, avoiding Euler chains and ambiguous imported-bone axes.
    void Arm(Transform upper,Transform fore,Transform hand,Vector3 target,Vector3 pole)
    {
        Vector3 a=upper.localPosition, v=target-a;
        float d=Mathf.Clamp(v.magnitude,.04f,.735f); Vector3 n=v.normalized;
        float along=(.36f*.36f-.38f*.38f+d*d)/(2*d);
        Vector3 bend=Vector3.ProjectOnPlane(pole,n).normalized;
        Vector3 elbow=a+n*along+bend*Mathf.Sqrt(Mathf.Max(0,.36f*.36f-along*along));
        Vector3 end=a+n*d;
        upper.localRotation=Quaternion.FromToRotation(Vector3.up,elbow-a);
        fore.rotation=chest.rotation*Quaternion.FromToRotation(Vector3.up,end-elbow);
        hand.rotation=root.rotation;
    }
    static float Ease(float t) => Mathf.SmoothStep(0,1,Mathf.Clamp01(t));
    public void Pose(float reload,float aim,float recoil,float walk,bool arrowVisible)
    {
        float reach=Mathf.Sin(Ease(Mathf.Min(reload*2,1))*Mathf.PI*.5f)*(1-Ease((reload-.5f)*2));
        float draw=Ease(aim);
        pelvis.localPosition=new Vector3(0,.66f+Mathf.Abs(Mathf.Sin(walk))*.025f,0);
        chest.localRotation=Quaternion.Euler(-recoil*10,reach*16-draw*12,0);
        // Lift the face slightly toward the elevated camera; recoil looks up,
        // matching the concept without changing hand targets or attack timing.
        head.localRotation=Quaternion.Euler(-6-recoil*8,-reach*12+draw*12,reach*-7);
        Vector3 bowHand=Vector3.Lerp(new Vector3(-.44f,-.16f,.32f),new Vector3(-.4f,.12f,.59f),draw);
        bowHand.z-=recoil*.08f;
        Vector3 nock=Vector3.Lerp(new Vector3(-.34f,-.12f,.28f),new Vector3(-.4f,.12f,-.1f),draw);
        nock=Vector3.Lerp(nock,new Vector3(.32f,.55f,-.27f),reach);
        nock+=new Vector3(.24f,0,-.09f)*recoil;
        Arm(leftUpper,leftFore,leftHand,chest.InverseTransformVector(root.TransformDirection(bowHand)),new Vector3(-1,-.5f,0));
        Arm(rightUpper,rightFore,rightHand,chest.InverseTransformVector(root.TransformDirection(nock)),new Vector3(1,.5f,-.3f));
        bow.rotation=root.rotation*Quaternion.Euler(0,0,-12*(1-draw));
        arrow.gameObject.SetActive(arrowVisible);
        arrow.position=rightHand.position;
        Vector3 direction=Vector3.Lerp(root.forward,bow.position+root.forward*.1f-rightHand.position,1-reach*.85f).normalized;
        arrow.rotation=Quaternion.LookRotation(direction,root.up);
        Vector3 stringNock=Vector3.Lerp(new Vector3(0,0,-.18f),bow.InverseTransformPoint(rightHand.position),draw);
        StretchString(stringTop,new Vector3(-.19f,.67f,-.18f),stringNock);
        StretchString(stringBottom,new Vector3(-.19f,-.67f,-.18f),stringNock);
        leftLeg.localRotation=Quaternion.Euler(180+Mathf.Sin(walk)*22,0,-5);
        rightLeg.localRotation=Quaternion.Euler(180-Mathf.Sin(walk)*22,0,5);
        leftShin.localRotation=Quaternion.Euler(-Mathf.Max(0,Mathf.Sin(walk))*22,0,0);
        rightShin.localRotation=Quaternion.Euler(-Mathf.Max(0,-Mathf.Sin(walk))*22,0,0);
    }
    static void StretchString(Transform t,Vector3 a,Vector3 b)
    {
        t.localPosition=a; t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        t.localScale=new Vector3(1,(b-a).magnitude,1);
    }
    public void Collapse(float t)
    {
        pelvis.localPosition=new Vector3(0,Mathf.Lerp(.66f,.16f,t),0);
        pelvis.localRotation=Quaternion.Euler(0,0,t*75); arrow.gameObject.SetActive(false);
    }
    public void Dispose()
    {
        foreach(var mesh in meshes) Object.Destroy(mesh);
        foreach(var mat in materials) Object.Destroy(mat);
    }
    // Flat-shaded low-poly pieces are combined per moving part/material.
    sealed class Geometry
    {
        readonly List<Vector3> vertices=new(); readonly List<int> triangles=new();
        void Tri(Vector3 a,Vector3 b,Vector3 c)
        {
            int n=vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
            triangles.Add(n); triangles.Add(n+1); triangles.Add(n+2);
        }
        public void Panel(Vector3 a,Vector3 b,Vector3 c)
        { Tri(a,b,c); Tri(c,b,a); }
        public void ChamferedBlock(Vector3 center,Vector3 radius)
        {
            // Four octagonal rings give a broad forehead and flat cheeks, with
            // beveled corners rather than a spherical cranium or a sharp cube.
            Vector2[] outline={new(1,.65f),new(.65f,1),new(-.65f,1),new(-1,.65f),new(-1,-.65f),new(-.65f,-1),new(.65f,-1),new(1,-.65f)};
            float[] heights={-1,-.68f,.68f,1};
            Vector3 P(int r,int s)
            {
                float width=r==0 || r==3 ? .72f : 1;
                Vector2 p=outline[s%8]*width;
                return center+Vector3.Scale(radius,new Vector3(p.x,heights[r],p.y));
            }
            for(int r=0;r<3;r++) for(int s=0;s<8;s++)
            { Tri(P(r,s),P(r+1,s),P(r,s+1)); Tri(P(r,s+1),P(r+1,s),P(r+1,s+1)); }
            for(int s=0;s<8;s++)
            { Tri(center-Vector3.up*radius.y,P(0,s),P(0,s+1)); Tri(center+Vector3.up*radius.y,P(3,s+1),P(3,s)); }
        }
        public void Ellipsoid(Vector3 center,Vector3 radius,int sides=8,int rings=4)
        {
            Vector3 P(int r,int s)
            {
                float theta=Mathf.PI*r/rings, phi=Mathf.PI*2*s/sides;
                return center+Vector3.Scale(radius,new Vector3(Mathf.Sin(theta)*Mathf.Cos(phi),Mathf.Cos(theta),Mathf.Sin(theta)*Mathf.Sin(phi)));
            }
            for(int r=0;r<rings;r++) for(int s=0;s<sides;s++)
            { Tri(P(r,s),P(r,s+1),P(r+1,s)); Tri(P(r,s+1),P(r+1,s+1),P(r+1,s)); }
        }
        public void Bone(Vector3 a,Vector3 b,float radius)
        {
            Quaternion q=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
            for(int i=0;i<6;i++)
            {
                float t=i*Mathf.PI/3, u=(i+1)*Mathf.PI/3;
                Vector3 v=q*new Vector3(Mathf.Cos(t)*radius,0,Mathf.Sin(t)*radius), w=q*new Vector3(Mathf.Cos(u)*radius,0,Mathf.Sin(u)*radius);
                Tri(a+v,b+v,a+w); Tri(a+w,b+v,b+w); Tri(a,a+w,a+v); Tri(b,b+v,b+w);
            }
        }
        public Mesh Build(string name)
        {
            var mesh=new Mesh {name=name}; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
