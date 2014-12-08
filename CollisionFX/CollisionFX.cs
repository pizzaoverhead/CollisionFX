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
        public float sparkLightIntensity = 0.05f;
        [KSPField]
        public float minScrapeSpeed = 1f;

        public float pitchRange = 0.3f;
        public float scrapeFadeSpeed = 5f;

        private GameObject fx;
        private ModuleWheel moduleWheel = null;
        private WheelCollider wheelCollider = null;
        private FXGroup ScrapeSounds = new FXGroup("ScrapeSounds");
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
            SetupAudio();

            GameEvents.onGamePause.Add(OnPause);
            GameEvents.onGameUnpause.Add(OnUnpause);

            if (part.Modules.Contains("ModuleWheel")) // Suppress the log message on failure.
                moduleWheel = part.Modules["ModuleWheel"] as ModuleWheel;

            wheelCollider = part.gameObject.GetComponent<WheelCollider>();

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
            fx = (GameObject)GameObject.Instantiate(UnityEngine.Resources.Load("Effects/fx_exhaustSparks_flameout"));
            fx.transform.parent = part.transform;
            fx.transform.position = part.transform.position;
            fx.particleEmitter.localVelocity = Vector3.zero;
            fx.particleEmitter.useWorldSpace = true;
            fx.particleEmitter.emit = false;
            fx.particleEmitter.minEnergy = 0;
            fx.particleEmitter.minEmission = 0;
        }

        private void SetupLight()
        {
            scrapeLight = fx.AddComponent<Light>();
            scrapeLight.type = LightType.Point;
            scrapeLight.range = 3f;
            scrapeLight.shadows = LightShadows.None;
            scrapeLight.enabled = false;
        }

        private void SetupAudio()
        {
            if (scrapeSparks)
            {
                if (ScrapeSounds == null)
                {
                    Debug.LogError("CollisionFX: Component was null");
                    return;
                }
                part.fxGroups.Add(ScrapeSounds);
                ScrapeSounds.audio = gameObject.AddComponent<AudioSource>();
                ScrapeSounds.audio.clip = GameDatabase.Instance.GetAudioClip(scrapeSound);
                if (ScrapeSounds.audio.clip == null)
                {
                    Debug.LogError("CollisionFX: Unable to load scrapeSound " + scrapeSound);
                    scrapeSparks = false;
                    return;
                }
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
        }

        private void OnUnpause()
        {
            paused = false;
        }

        private void OnDestroy()
        {
            if (ScrapeSounds != null && ScrapeSounds.audio != null)
                ScrapeSounds.audio.Stop();
            GameEvents.onGamePause.Remove(OnPause);
            GameEvents.onGameUnpause.Remove(OnUnpause);
        }

        // Not called on parts where physicalSignificance = false. Check the parent part instead.
        public void OnCollisionEnter(Collision c)
        {
            if (paused) return;

            if (c.relativeVelocity.magnitude > 3)
            {
                var cInfo = GetClosestChild(part, c.contacts[0].point + (part.rigidbody.velocity * Time.deltaTime));
                if (cInfo.CollisionFX != null)
                    cInfo.CollisionFX.Impact(cInfo.IsWheel);
                else
                    Impact(IsCollidingWheel(c.contacts[0].point));
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
                BangSound.audio.Stop();
            }
            else
            {
                // Shift the pitch randomly each time so that the impacts don't all sound the same.
                BangSound.audio.pitch = UnityEngine.Random.Range(1 - pitchRange, 1 + pitchRange);
                BangSound.audio.Play();
                if (WheelImpactSound != null)
                {
                    WheelImpactSound.audio.Stop();
                }
            }
        }

        // Not called on parts where physicalSignificance = false. Check the parent part instead.
        public void OnCollisionStay(Collision c)
        {
            if (!scrapeSparks) return;

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
            float parentDistance = Vector3.Distance(parent.collider.ClosestPointOnBounds(p), p);
            float minDistance = parentDistance;
            CollisionFX closestChild = null;
            bool isWheel = false;

            foreach (Part child in parent.children)
            {
                if (child != null && child.collider != null &&
                    (child.physicalSignificance == Part.PhysicalSignificance.NONE))
                {
                    float childDistance = Vector3.Distance(child.collider.ClosestPointOnBounds(p), p);
                    var wheel = child.GetComponent<WheelCollider>();
                    if (wheel != null)
                    {
                        float wheelDistance = Vector3.Distance(wheel.ClosestPointOnBounds(p), p);
                        if (wheelDistance < childDistance)
                        {
                            isWheel = true;
                            childDistance = wheelDistance;
                        }
                    }

                    if (childDistance < minDistance)
                    {
                        var cfx = child.GetComponent<CollisionFX>();
                        if (cfx != null)
                        {
                            minDistance = childDistance;
                            closestChild = cfx;
                        }
                        else isWheel = false;
                    }
                    else isWheel = false;
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

            if (fx != null)
                fx.transform.LookAt(c.transform);
            if (part.rigidbody == null) // Part destroyed?
            {
                StopScrape();
                return;
            }

            float m = c.relativeVelocity.magnitude;
            ScrapeParticles(m, c.contacts[0].point + (part.rigidbody.velocity * Time.deltaTime));
            ScrapeSound(m);

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
                ScrapeSounds.audio.Stop();
                scrapeLight.enabled = false;
#if DEBUG
                spheres[0].transform.position = part.transform.position;
                spheres[0].renderer.enabled = true;
                spheres[1].renderer.enabled = false;
#endif
            }
        }

        private void ScrapeParticles(float speed, Vector3 contactPoint)
        {
            if (speed > minScrapeSpeed)
            {
                fx.transform.position = contactPoint;
                fx.particleEmitter.maxEnergy = speed / 10;                          // Values determined
                fx.particleEmitter.maxEmission = Mathf.Clamp((speed * 2), 0, 75);   // via experimentation.
                fx.particleEmitter.Emit();
                fx.particleEmitter.worldVelocity = -part.rigidbody.velocity;
                scrapeLight.enabled = true;
                scrapeLight.color = Color.Lerp(lightColor1, lightColor2, UnityEngine.Random.Range(0f, 1f));
                scrapeLight.intensity = UnityEngine.Random.Range(0f, sparkLightIntensity);
            }
            else
                scrapeLight.enabled = false;
        }

        private void ScrapeSound(float speed)
        {
            if (speed > minScrapeSpeed)
            {
                if (!ScrapeSounds.audio.isPlaying)
                    ScrapeSounds.audio.Play();
                ScrapeSounds.audio.pitch = 1 + Mathf.Log(speed) / 5;

                if (speed < scrapeFadeSpeed)
                {
                    // Fade out at low speeds.
                    ScrapeSounds.audio.volume = speed / scrapeFadeSpeed * volume * GameSettings.SHIP_VOLUME;
                }
                else
                    ScrapeSounds.audio.volume = volume * GameSettings.SHIP_VOLUME;
            }
            else
                ScrapeSounds.audio.Stop();
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
