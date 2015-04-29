using System;
using UnityEngine;

namespace CollisionFX
{
    public class CollisionFX : PartModule
    {
        [KSPField]
        public float volume = 0.5f;
        [KSPField]
        public bool scrapeSparks = true;
        [KSPField]
        public string collisionSound = String.Empty;
        [KSPField]
        public string wheelImpactSound = String.Empty;
        [KSPField]
        public string scrapeSound = String.Empty;
        [KSPField]
        public string sparkSound = String.Empty;
        [KSPField]
        public float sparkLightIntensity = 0.05f;
        [KSPField]
        public float minScrapeSpeed = 1f;

        public float pitchRange = 0.3f;
        public float scrapeFadeSpeed = 5f;

        private GameObject sparkFx;
        private GameObject dustFx;
        ParticleAnimator dustAnimator;
        private ModuleWheel moduleWheel = null;
        private WheelCollider wheelCollider = null;
        private FXGroup ScrapeSounds = new FXGroup("ScrapeSounds");
        private FXGroup SparkSounds = new FXGroup("SparkSounds");
        private FXGroup BangSound = new FXGroup("BangSound");
        private FXGroup WheelImpactSound = null;
        private Light scrapeLight;
        private Color lightColor1 = new Color(254, 226, 160); // Tan / light orange
        private Color lightColor2 = new Color(239, 117, 5); // Red-orange.

#if DEBUG
        private GameObject[] spheres = new GameObject[3];
#endif

        public class CollisionInfo
        {
            public CollisionFX CollisionFX;
            public bool IsWheel;

            public CollisionInfo(CollisionFX collisionFX, bool isWheel)
            {
                CollisionFX = collisionFX;
                IsWheel = isWheel;
            }
        }

        public override void OnStart(StartState state)
        {
            if (state == StartState.Editor || state == StartState.None) return;

            if (scrapeSparks)
            {
                SetupParticles();
                SetupLight();
            }

            if (part.Modules.Contains("ModuleWheel")) // Suppress the log message on failure.
                moduleWheel = part.Modules["ModuleWheel"] as ModuleWheel;
            wheelCollider = part.GetComponent<WheelCollider>();
            if (wheelCollider == null)
            {
                ModuleLandingGear gear = part.GetComponent<ModuleLandingGear>();
                if (gear != null)
                {
                    wheelCollider = gear.wheelCollider;
                }
            }

            SetupAudio();

            GameEvents.onGamePause.Add(OnPause);
            GameEvents.onGameUnpause.Add(OnUnpause);


#if DEBUG
            for (int i = 0; i < spheres.Length; i++)
            {
                spheres[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(spheres[i].collider);
            }
            spheres[0].renderer.material.color = Color.red;
            spheres[1].renderer.material.color = Color.green;
#endif
        }

        private void SetupParticles()
        {
            //ScreenMessages.PostScreenMessage(particleTypes[currentParticle]);

            sparkFx = (GameObject)GameObject.Instantiate(UnityEngine.Resources.Load("Effects/fx_exhaustSparks_flameout"));
            sparkFx.transform.parent = part.transform;
            sparkFx.transform.position = part.transform.position;
            sparkFx.particleEmitter.localVelocity = Vector3.zero;
            sparkFx.particleEmitter.useWorldSpace = true;
            sparkFx.particleEmitter.emit = false;
            sparkFx.particleEmitter.minEnergy = 0;
            sparkFx.particleEmitter.minEmission = 0;

            dustFx = (GameObject)GameObject.Instantiate(UnityEngine.Resources.Load("Effects/fx_smokeTrail_light"));
            dustFx.transform.parent = part.transform;
            dustFx.transform.position = part.transform.position;
            dustFx.particleEmitter.localVelocity = Vector3.zero;
            dustFx.particleEmitter.useWorldSpace = true;
            dustFx.particleEmitter.emit = false;
            dustFx.particleEmitter.minEnergy = 0;
            dustFx.particleEmitter.minEmission = 0;
            dustAnimator = dustFx.particleEmitter.GetComponent<ParticleAnimator>();
        }

        private void SetupLight()
        {
            scrapeLight = sparkFx.AddComponent<Light>();
            scrapeLight.type = LightType.Point;
            scrapeLight.range = 3f;
            scrapeLight.shadows = LightShadows.None;
            scrapeLight.enabled = false;
        }

        private void SetupAudio()
        {
            if (scrapeSparks)
            {
                if (SparkSounds == null)
                {
                    Debug.LogError("CollisionFX: Component was null");
                    return;
                }
                part.fxGroups.Add(SparkSounds);
                SparkSounds.audio = gameObject.AddComponent<AudioSource>();
                SparkSounds.audio.clip = GameDatabase.Instance.GetAudioClip(sparkSound);
                if (SparkSounds.audio.clip == null)
                {
                    Debug.LogError("CollisionFX: Unable to load sparkSound \"" + sparkSound + "\"");
                    scrapeSparks = false;
                    return;
                }
                SparkSounds.audio.dopplerLevel = 0f;
                SparkSounds.audio.rolloffMode = AudioRolloffMode.Logarithmic;
                SparkSounds.audio.Stop();
                SparkSounds.audio.loop = true;
                SparkSounds.audio.volume = volume * GameSettings.SHIP_VOLUME;
                SparkSounds.audio.time = UnityEngine.Random.Range(0, SparkSounds.audio.clip.length);
            }

            if (ScrapeSounds == null)
            {
                Debug.LogError("CollisionFX: ScrapeSounds was null");
                return;
            }
            part.fxGroups.Add(ScrapeSounds);
            ScrapeSounds.audio = gameObject.AddComponent<AudioSource>();
            ScrapeSounds.audio.clip = GameDatabase.Instance.GetAudioClip(scrapeSound);
            if (ScrapeSounds.audio.clip == null)
            {
                Debug.LogError("CollisionFX: Unable to load scrapeSound \"" + scrapeSound + "\"");
            }
            else
            {
                ScrapeSounds.audio.dopplerLevel = 0f;
                ScrapeSounds.audio.rolloffMode = AudioRolloffMode.Logarithmic;
                ScrapeSounds.audio.Stop();
                ScrapeSounds.audio.loop = true;
                ScrapeSounds.audio.volume = volume * GameSettings.SHIP_VOLUME;
                ScrapeSounds.audio.time = UnityEngine.Random.Range(0, ScrapeSounds.audio.clip.length);
            }

            part.fxGroups.Add(BangSound);
            BangSound.audio = gameObject.AddComponent<AudioSource>();
            BangSound.audio.clip = GameDatabase.Instance.GetAudioClip(collisionSound);
            BangSound.audio.dopplerLevel = 0f;
            BangSound.audio.rolloffMode = AudioRolloffMode.Logarithmic;
            BangSound.audio.Stop();
            BangSound.audio.loop = false;
            BangSound.audio.volume = GameSettings.SHIP_VOLUME;

            if (wheelCollider != null && !String.IsNullOrEmpty(wheelImpactSound))
            {
                WheelImpactSound = new FXGroup("WheelImpactSound");
                part.fxGroups.Add(WheelImpactSound);
                WheelImpactSound.audio = gameObject.AddComponent<AudioSource>();
                WheelImpactSound.audio.clip = GameDatabase.Instance.GetAudioClip(wheelImpactSound);
                WheelImpactSound.audio.dopplerLevel = 0f;
                WheelImpactSound.audio.rolloffMode = AudioRolloffMode.Logarithmic;
                WheelImpactSound.audio.Stop();
                WheelImpactSound.audio.loop = false;
                WheelImpactSound.audio.volume = GameSettings.SHIP_VOLUME;
            }
        }

        bool paused = false;
        private void OnPause()
        {
            paused = true;
            if (scrapeSparks)
                ScrapeSounds.audio.Stop();
            if (SparkSounds != null && SparkSounds.audio != null)
                SparkSounds.audio.Stop();

        }

        private void OnUnpause()
        {
            paused = false;
        }

        private void OnDestroy()
        {
            if (ScrapeSounds != null && ScrapeSounds.audio != null)
                ScrapeSounds.audio.Stop();
            if (SparkSounds != null && SparkSounds.audio != null)
                SparkSounds.audio.Stop();
            GameEvents.onGamePause.Remove(OnPause);
            GameEvents.onGameUnpause.Remove(OnUnpause);
        }

        // Not called on parts where physicalSignificance = false. Check the parent part instead.
        public void OnCollisionEnter(Collision c)
        {
            if (paused) return;
            if (c.relativeVelocity.magnitude > 3)
            {
                if (c.contacts.Length == 0)
                    return;

                var cInfo = GetClosestChild(part, c.contacts[0].point + (part.rigidbody.velocity * Time.deltaTime));
                if (cInfo.CollisionFX != null)
                {
                    cInfo.CollisionFX.Impact(cInfo.IsWheel);
                }
                else
                {
                    Impact(IsCollidingWheel(c.contacts[0].point));
                }
            }
        }

        /// <summary>
        /// This part has come into contact with something. Play an appropriate sound.
        /// </summary>
        public void Impact(bool isWheel)
        {
            if (isWheel && WheelImpactSound != null)
            {
                WheelImpactSound.audio.pitch = UnityEngine.Random.Range(1 - pitchRange, 1 + pitchRange);
                WheelImpactSound.audio.Play();
                if (BangSound != null)
                {
                    BangSound.audio.Stop();
                }
            }
            else
            {
                if (BangSound != null)
                {
                    if (BangSound.audio != null)
                    {
                        // Shift the pitch randomly each time so that the impacts don't all sound the same.
                        BangSound.audio.pitch = UnityEngine.Random.Range(1 - pitchRange, 1 + pitchRange);
                        BangSound.audio.Play();
                    }
                }
                if (WheelImpactSound != null)
                {
                    WheelImpactSound.audio.Stop();
                }
            }
        }

        // Not called on parts where physicalSignificance = false. Check the parent part instead.
        public void OnCollisionStay(Collision c)
        {
            if (!scrapeSparks || paused) return;

            // Contact points are from the previous frame. Add the velocity to get the correct position.
            var cInfo = GetClosestChild(part, c.contacts[0].point + (part.rigidbody.velocity * Time.deltaTime));
            if (cInfo.CollisionFX != null)
            {
                StopScrape();
                foreach (var p in part.children)
                {
                    var colFx = p.GetComponent<CollisionFX>();
                    if (colFx != null)
                        colFx.StopScrape();
                }
                cInfo.CollisionFX.Scrape(c);
                return;
            }

            Scrape(c);
        }

        /// <summary>
        /// Checks whether the collision is happening on this part's wheel.
        /// </summary>
        /// <returns></returns>
        private bool IsCollidingWheel(Vector3 collisionPoint)
        {
            if (wheelCollider == null) return false;
            float wheelDistance = Vector3.Distance(wheelCollider.ClosestPointOnBounds(collisionPoint), collisionPoint);
            float partDistance = Vector3.Distance(part.collider.ClosestPointOnBounds(collisionPoint), collisionPoint);
            return wheelDistance < partDistance;
        }

        /// <summary>
        /// Searches child parts for the nearest instance of this class to the given point.
        /// </summary>
        /// <remarks>Parts with physicalSignificance=false have their collisions detected by the parent part.
        /// To identify which part is the source of a collision, check which part the collision is closest to.</remarks>
        /// <param name="parent">The parent part whose children should be tested.</param>
        /// <param name="p">The point to test the distance from.</param>
        /// <returns>The nearest child part's CollisionFX module, or null if the parent part is nearest.</returns>
        private static CollisionInfo GetClosestChild(Part parent, Vector3 p)
        {
            float parentDistance = Vector3.Distance(parent.transform.position, p);
            float minDistance = parentDistance;
            CollisionFX closestChild = null;
            bool isWheel = false;

            foreach (Part child in parent.children)
            {
                if (child != null && child.collider != null &&
                    (child.physicalSignificance == Part.PhysicalSignificance.NONE))
                {
                    float childDistance = Vector3.Distance(child.transform.position, p);
                    var cfx = child.GetComponent<CollisionFX>();
                    if (cfx != null)
                    {
                        if (cfx.wheelCollider != null)
                        {
                            float wheelDistance = Vector3.Distance(cfx.wheelCollider.ClosestPointOnBounds(p), p);
                            if (wheelDistance < childDistance)
                            {
                                isWheel = true;
                                childDistance = wheelDistance;
                            }
                        }

                        if (childDistance < minDistance)
                        {
                            minDistance = childDistance;
                            closestChild = cfx;
                        }
                        else
                            isWheel = false;
                    }
                    else
                        isWheel = false;
                }
            }
            return new CollisionInfo(closestChild, isWheel);
        }

        /// <summary>
        /// This part is moving against another surface. Create sound, light and particle effects.
        /// </summary>
        /// <param name="c"></param>
        public void Scrape(Collision c)
        {
            if (paused || part == null)
            {
                StopScrape();
                return;
            }
            if (wheelCollider != null)
            {
                // Has a wheel collider.
                if (moduleWheel != null)
                {
                    // Has a wheel module.
                    if (!moduleWheel.isDamaged)
                    {
                        // Has an intact wheel.
                        StopScrape();
                        return;
                    }
                }
                else
                {
                    // Has a wheel collider but not a wheel (hover parts, some landing gear).
                    StopScrape();
                    return;
                }
            }

            if (sparkFx != null)
                sparkFx.transform.LookAt(c.transform);
            if (part.rigidbody == null) // Part destroyed?
            {
                StopScrape();
                return;
            }

            float m = c.relativeVelocity.magnitude;
            ScrapeParticles(m, c.contacts[0].point + (part.rigidbody.velocity * Time.deltaTime), c.collider, c.gameObject);
            ScrapeSound(ScrapeSounds, m);
            if (CanSpark(c.collider, c.gameObject))
                ScrapeSound(SparkSounds, m);
            else
                SparkSounds.audio.Stop();

#if DEBUG
            spheres[0].renderer.enabled = false;
            spheres[1].renderer.enabled = true;
            spheres[1].transform.position = part.transform.position;
            spheres[2].transform.position = c.contacts[0].point + (part.rigidbody.velocity * Time.deltaTime);
#endif
        }

        public void StopScrape()
        {
            if (scrapeSparks)
            {
                if (SparkSounds != null && SparkSounds.audio == null)
                    SparkSounds.audio.Stop();
                scrapeLight.enabled = false;
#if DEBUG
                spheres[0].transform.position = part.transform.position;
                spheres[0].renderer.enabled = true;
                spheres[1].renderer.enabled = false;
#endif
            }

            if (ScrapeSounds != null && ScrapeSounds.audio != null)
                ScrapeSounds.audio.Stop();
        }

        /* Kerbin natural biomes:
            Water           Sand. If we're hitting a terrain collider here, it will be sandy.
            Grasslands      Dirt
            Highlands       Dirt (dark?)
            Shores          Dirt (too grassy for sand)
            Mountains       Rock particles?
            Deserts         Sand
            Badlands        ?
            Tundra          Dirt?
            Ice Caps        Snow
         */

        public string GetCurrentBiomeName()
        {
            CBAttributeMapSO biomeMap = FlightGlobals.currentMainBody.BiomeMap;
            CBAttributeMapSO.MapAttribute mapAttribute = biomeMap.GetAtt(vessel.latitude * Mathf.Deg2Rad, vessel.longitude * Mathf.Deg2Rad);
            return mapAttribute.name;
        }

        public Color genericDustColour = new Color(0.8f, 0.8f, 0.8f, 0.007f); // Grey 210 210 210
        public Color dirtColour = new Color(0.65f, 0.48f, 0.34f, 0.05f); // Brown 165, 122, 88
        public Color lightDirtColour = new Color(0.65f, 0.52f, 0.34f, 0.05f); // Brown 165, 132, 88
        public Color sandColour = new Color(0.80f, 0.68f, 0.47f, 0.05f); // Light brown 203, 173, 119
        public Color snowColour = new Color(0.90f, 0.94f, 1f, 0.05f); // Blue-white 230, 250, 255
        public Color GetBiomeColour(Collider c)
        {
            switch (FlightGlobals.ActiveVessel.mainBody.name)
            {
                case "Kerbin":
                    if (IsPQS(c))
                    {
                        string biome = GetCurrentBiomeName();
                        switch (biome)
                        {
                            case "Water":
                                return sandColour;
                            case "Grasslands":
                                return dirtColour;
                            case "Highlands":
                                return dirtColour;
                            case "Shores":
                                return lightDirtColour;
                            case "Mountains":
                                return dirtColour;
                            case "Deserts":
                                return sandColour;
                            case "Badlands":
                                return dirtColour;
                            case "Tundra":
                                return dirtColour;
                            case "Ice Caps":
                                return snowColour;
                            default:
                                return dirtColour;
                        }
                    }
                    else
                    {
                        return genericDustColour;
                    }
                default:
                    return genericDustColour;
            }
        }

        public bool IsPQS(Collider c)
        {
            if (c == null) return false;
            // Test for PQS: Name in the form "Ab0123456789".
            Int64 n;
            return c.name.Length == 12 && Int64.TryParse(c.name.Substring(2, 10), out n);
        }

        public bool IsRagdoll(GameObject g)
        {
            // TODO: Find a better way of doing this.
            return g.name.StartsWith("bn_") || g.name.StartsWith("be_");
        }

        /// <summary>
        /// Whether the object collided with produces sparks.
        /// </summary>
        /// <returns>True if the object is a CollisionFX with scrapeSparks or a non-CollisionFX object.</returns>
        public bool TargetScrapeSparks(GameObject collidedWith)
        {
            CollisionFX objectCollidedWith = collidedWith.GetComponent<CollisionFX>();
            return objectCollidedWith == null ? true : objectCollidedWith.scrapeSparks;
        }

        public bool CanSpark(Collider c, GameObject collidedWith)
        {
            return scrapeSparks && TargetScrapeSparks(collidedWith) && FlightGlobals.ActiveVessel.atmDensity > 0 && FlightGlobals.currentMainBody.atmosphereContainsOxygen && !IsPQS(c);
        }

        private void ScrapeParticles(float speed, Vector3 contactPoint, Collider col, GameObject collidedWith)
        {
            if (speed > minScrapeSpeed)
            {
                if (!IsPQS(col) && !wheelCollider && TargetScrapeSparks(collidedWith) && !IsRagdoll(collidedWith))
                {
                    if (FlightGlobals.ActiveVessel.atmDensity > 0 && FlightGlobals.currentMainBody.atmosphereContainsOxygen)
                    {
                        sparkFx.transform.position = contactPoint;
                        sparkFx.particleEmitter.maxEnergy = speed / 10;                          // Values determined
                        sparkFx.particleEmitter.maxEmission = Mathf.Clamp((speed * 2), 0, 75);   // via experimentation.
                        sparkFx.particleEmitter.Emit();
                        sparkFx.particleEmitter.worldVelocity = -part.rigidbody.velocity;
                        scrapeLight.enabled = true;
                        scrapeLight.color = Color.Lerp(lightColor1, lightColor2, UnityEngine.Random.Range(0f, 1f));
                        scrapeLight.intensity = UnityEngine.Random.Range(0f, sparkLightIntensity);
                    }
                }
                
                dustFx.transform.position = contactPoint;
                dustFx.particleEmitter.maxEnergy = speed / 10;                          // Values determined
                dustFx.particleEmitter.maxEmission = Mathf.Clamp((speed * 2), 0, 75);   // via experimentation.
                dustFx.particleEmitter.Emit();
                //dustFx.particleEmitter.worldVelocity = -part.rigidbody.velocity;
                // Set dust biome colour.
                if (dustAnimator != null)
                {
                    Color c = GetBiomeColour(col);
                    Color[] colors = dustAnimator.colorAnimation;
                    colors[0] = c;
                    colors[1] = c;
                    colors[2] = c;
                    colors[3] = c;
                    colors[4] = c;
                    dustAnimator.colorAnimation = colors;
                }

            }
            else
                scrapeLight.enabled = false;
        }

        private void ScrapeSound(FXGroup sound, float speed)
        {
            if (speed > minScrapeSpeed)
            {
                if (!sound.audio.isPlaying)
                    sound.audio.Play();
                sound.audio.pitch = 1 + Mathf.Log(speed) / 5;

                if (speed < scrapeFadeSpeed)
                {
                    // Fade out at low speeds.
                    sound.audio.volume = speed / scrapeFadeSpeed * volume * GameSettings.SHIP_VOLUME;
                }
                else
                    sound.audio.volume = volume * GameSettings.SHIP_VOLUME;
            }
            else
                sound.audio.Stop();
        }

        private void OnCollisionExit(Collision c)
        {
            StopScrape();
            if (c.contacts.Length > 0)
            {
                var cInfo = GetClosestChild(part, c.contacts[0].point + (part.rigidbody.velocity * Time.deltaTime));
                if (cInfo.CollisionFX != null)
                    cInfo.CollisionFX.StopScrape();
            }
        }
    }
}

