using UnityEngine;
using System.Collections;


namespace Enviro
{
 
  public class Lightning : MonoBehaviour, ILightningEffect
  {
    public void CastBolt(Vector3 origin, Vector3 target)
    {
        this.transform.position = origin;
        this.target = target;
		    this.CastBolt();
    }

    public float flashIntensity = 50f;
    public Vector3 target;
    private LineRenderer lineRend;
    public Light myLight;
    public Material planeMat;
    public int arcs = 20;
    public float arcLength = 100.0f;
    public float arcVariation = 1.0f;
    public float inaccuracy = 0.5f;
    public int splits = 4;
    public int maxSplits = 24;
    private int splitCount = 0;
    public float splitLength = 100.0f;
    public float splitVariation = 1.0f;

    public Vector3 toTarget;
    private bool fadeOut;
    private float fadeTimer;

    void OnEnable () 
    {
        lineRend = gameObject.GetComponent<LineRenderer> ();
    }

    IEnumerator CreateLightningBolt()
    {
      myLight.enabled = false;
      lineRend.widthMultiplier = 10;
      planeMat.SetFloat("_Brightness", 1f);


      lineRend.SetPosition(0, transform.position);
      lineRend.positionCount = 2;
      lineRend.SetPosition(1, transform.position);
      Vector3 lastPoint = transform.position;
      float dist = Vector3.Distance(transform.position, target);

      float arcDist = dist / arcs;

      for (int i = 1; i < arcs; i++)
      {
        planeMat.SetFloat("_Brightness", Random.Range(0f,2f));
        lineRend.positionCount =  i + 1;
        Vector3 fwd = target - lastPoint;
        fwd.Normalize ();
        Vector3 pos = Randomize (fwd, inaccuracy);
        pos *= Random.Range (arcLength * arcVariation, arcLength) * (arcDist);
        pos += lastPoint;
        lineRend.SetPosition (i, pos);
       
      if (i % 2 == 0)
      {
        for (int s = 0; s <= splits; s++)
        { 
            if(splitCount < maxSplits)
            {
               StartCoroutine(CreateSplit(pos, target));
              yield return new WaitForSeconds(Random.Range(0.0001f,0.0002f));
            }
        }
      }
        //yield return null;
        lastPoint = pos;

      }
      yield return new WaitForSeconds(Random.Range(0.1f,0.2f));
      lineRend.SetPosition(arcs-1,target);

      //Animate Light and Main bolt
      myLight.transform.position = target;
      
      if(EnviroManager.instance.Camera != null)
      myLight.transform.LookAt(EnviroManager.instance.Camera.transform.position, Vector3.up);

      lineRend.material.SetFloat("_Brightness", this.flashIntensity);
      planeMat.SetFloat("_Brightness", 20f);
      myLight.enabled = true;
      yield return new WaitForSeconds(Random.Range(0.025f,0.035f));
      lineRend.material.SetFloat("_Brightness", 1f);
      planeMat.SetFloat("_Brightness", 1f);
      myLight.enabled = false;
      yield return new WaitForSeconds(Random.Range(0.025f,0.035f));
      lineRend.material.SetFloat("_Brightness", this.flashIntensity);
      planeMat.SetFloat("_Brightness", 20f);
      myLight.enabled = true;
      yield return new WaitForSeconds(Random.Range(0.025f,0.035f));
      lineRend.material.SetFloat("_Brightness", 1f);
      planeMat.SetFloat("_Brightness", 1f);
      myLight.enabled = false;
      yield return new WaitForSeconds(Random.Range(0.025f,0.035f));
      lineRend.material.SetFloat("_Brightness", this.flashIntensity);
      planeMat.SetFloat("_Brightness", 0f);
      myLight.enabled = true;
      yield return new WaitForSeconds(Random.Range(0.025f,0.035f));
      myLight.enabled = false;
      fadeTimer = 50f;
      fadeOut = true;
    }

    IEnumerator CreateSplit(Vector3 pos, Vector3 targetP)
    {
      splitCount++;
      GameObject split = new GameObject();
      split.transform.SetParent(transform);
      split.transform.position = pos;
      LineRenderer splitRenderer = split.AddComponent<LineRenderer>();
      splitRenderer.material = lineRend.material;
      splitRenderer.material.SetFloat("_Brightness", flashIntensity * 0.5f);
      splitRenderer.positionCount = 2;
      splitRenderer.SetPosition(0, split.transform.position);
      splitRenderer.SetPosition(1, split.transform.position);     
 
      //Set a random target 
      toTarget = targetP - pos; 
      toTarget = Vector3.Normalize(toTarget);
      //Vector3 posDown = new Vector3(toTarget.x,toTarget.y, toTarget.z * 0.1f);     
      Vector3 targetPos = (Random.insideUnitSphere * 500 + pos + toTarget * 1000);
      
      Vector3 lastPoint = split.transform.position;
      float dist = Vector3.Distance(split.transform.position, targetPos);

      float arcDist = dist / 32;

      for (int i = 1; i < 32; i++) 
      {
        splitRenderer.positionCount =  i + 1;
        Vector3 fwd = targetPos - lastPoint;
        fwd.Normalize ();
        Vector3 newPos = Randomize (fwd, inaccuracy);

        newPos *= Random.Range (splitLength * splitVariation, splitLength) * (arcDist);
        newPos += lastPoint;
        splitRenderer.SetPosition (i, newPos);
        lastPoint = newPos;
        //yield return null;
      }
      //splitRenderer.SetPosition(7,targetPos);
      yield return new WaitForSeconds(Random.Range(0.2f,0.5f));
      DestroyImmediate(split);
    }
 
    public void CastBolt()
    {
        lineRend.positionCount = 1;
        StartCoroutine(CreateLightningBolt());
    }
    private Vector3 Randomize (Vector3 newVector, float devation) {
        newVector += new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f)) * devation;
        newVector.Normalize();
        return newVector;
    }

    private void Update() 
    {
      if(fadeOut == true)
      {
        fadeTimer = Mathf.Lerp(fadeTimer,0f,10f * Time.deltaTime);
        lineRend.material.SetFloat("_Brightness", fadeTimer);

        if(fadeTimer <= 1f)
          {
            lineRend.positionCount = 1;
            fadeOut = false;
            DestroyImmediate(gameObject);
          }
      }
    }
 } 
}