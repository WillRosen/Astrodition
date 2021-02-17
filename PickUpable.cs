using System.Collections;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

using UnityEngine.UI;

/*
 * PickUpable is a class for all items in the game that the player can
 * hold in their hands.
 * This handles all networking/saveing/loading
*/
public class PickUpable : PhysicsItem
{

    [Header("Actions & Uses")]
    protected List<UserAction> primaryUses = new List<UserAction>();
    protected List<UserAction> secondaryUses = new List<UserAction>();

    protected int useIndex = 0;

    bool isCurrentPrimaryAction = default;
    bool canStartAction = default;
    UserAction currentAction = default;

    [Header("Inventory")]
    [SerializeField]
    Vector2 inventorySize = new Vector2(1, 1);
    [SerializeField]
    Sprite itemSprite = default;

    [SerializeField]
    protected bool itemInHands = false;
    protected bool itemInNetworkHands = false;
    bool rotatedItem;
    public Container isWithinContainer;



    [Header("Precision Placement")] 
    [SerializeField]
    protected float offset = default;
    [SerializeField]
    bool isPrecisionPlacing = default;

    [SerializeField]
    protected Vector3 rotationAxis;
    protected float currentRotation;

    [Header("Animation & Hands")]
    [SerializeField]
    Animator anim = default;
    [SerializeField]
    Transform leftHandAnim = default;
    [SerializeField]
    Transform rightHandAnim = default;
    [SerializeField]

    public enum HandConfig { Left, Right, Both, None }
    public bool ableToPickUp = true;
    public bool isHiddenFromWorld = false;
    float throwVelocity = default;

    [Header("Audio")]
    protected AudioSource itemAquireSound = default;
    protected AudioSource itemInInv = default;



    //Function inherits from Physics Item
    protected override void Start()
    {
        base.Start();

        if (container != null)
        {
            container.setContainerSprite(itemSprite);
        }

        itemAquireSound = makeAudioSourceFromGenericSounds(GameReference.self.genericSounds.itemAquire);
        itemInInv = makeAudioSourceFromGenericSounds(GameReference.self.genericSounds.itemInInv);

        //calculateOffset();

    }

    //Function inherits from Physics Item
    protected override void Awake()
    {

        base.Awake();

        anim = GetComponent<Animator>();

        if (anim != null)
        {
            anim.enabled = false;
        }
        else
        {
            //If no unique animation, use default
            anim = gameObject.AddComponent<Animator>();
            anim.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("DefaultAnimations/DefaultController");
            anim.enabled = false;
        }
        if (rotationAxis == Vector3.zero)
        {
            //If no specified rotation axis, up is default
            rotationAxis = Vector3.up;
        }
        
    }

    //Function inherits from Physics Item
    public override void setUpItem(int seed)
    {
        base.setUpItem(seed);
        container = transform.GetComponent<Container>();
        if (container != null)
        {
            container.setContainerSprite(itemSprite);
        }
    }

    //Hides item is player is in space
    public void setSpaceHide(bool hide) {
        if (hide) {
            hideFromWorld();
        }
        else {
            enableAllRenderers();//so item in hand does not have collider
        }
    }


    public void setAbleToPickup(bool val) {
        ableToPickUp = val;
    }


    public virtual void disableAnimation() {
        if (anim != null)
        {
            anim.enabled = false;
        }
    }
    public void enableAnimation() {
        if (anim != null)
        {
            anim.enabled = true;
        }
    }

    //Sets and syncs animation over network
    protected void setAnimStateAndSync(string s, bool val)
    {
        anim.SetBool(s, val);

        sendNetworkData("Anim," + s + "," + BoolToString(val));
    }

    //hide item if player is using an in game computer screen
    public void startedUsingScreen()
    {
        disableAllRenderers();
    }
    //unhide item if player stopped using an in game computer screen
    public void stoppedUsingScreen() {
        enableAllRenderers();
    }

    
    //takes hands off item and allows precision placement of the item
    public void startPrecisionPlace() {
       
         isPrecisionPlacing = true;
        disableAnimation();
        disableAllColliders();
        GameReference.self.playerIK.setLeftHand(null);
        GameReference.self.playerIK.setRightHand(null);
    }

    //used to change the rotation when placing an item
    public void changePlaceRotation(float value) {
        currentRotation += value;
    }

    //place has been cancelled, re assign hands and resume animation
    public void stopPrecisionPlace() {
        isPrecisionPlacing = false;
        enableAnimation();
        GameReference.self.playerIK.setLeftHand(getLeftHand());
        GameReference.self.playerIK.setRightHand(getRightHand());
    }

    //does the final placing of the item
    void makePrecisionPlace()
    {

        isPrecisionPlacing = false;
        dropFromHands();

        SyncUntilRest();

        GameReference.self.playerInventory.objectDeleted(transform);
        enableAllColliders();
    }


    // Update is called once per frame | Function inherits from Physics Item
    protected override void Update()
    {
        base.Update();
        if (itemInHands)
        {
            whileItemInHands();
        }
    }

    //sets the rotation of the item in the inventory eg | or __ (x,y) or (y,x) dimentions
    public void setRotation(bool rotated) {
        rotatedItem = rotated;
        sendNetworkData("InvRot," + BoolToString(rotatedItem));
    }
    public bool getIsRotated() {
        return rotatedItem;
    }
    //inventory size is dependant on if it is rotated
    public Vector2 getInventorySize() {
        return rotatedItem ? new Vector2(inventorySize.y, inventorySize.x) : inventorySize;
    }

    //called when put inside contianer, used to hide, disable physics and such
    public virtual void onPutInContainer(Container c) {

        if (isWithinContainer != null) {
            //if (c != isWithinContainer) {
            isWithinContainer.removeByID(getID(), getInventorySize());
          //  }
        }

        if (itemInInv != null)
        {
           // itemInInv.Play();
        }
        isWithinContainer = c;
        
        if (isInHands()) {
            onExitHands(false);
        }
        hideFromWorld();

        setController(Controller.Local);

        stopSyncingUntilRest();

        sendNetworkData("InInv," +FloatToString(c.GetComponent<Savable>().getID()));
    }


    //called when put inside contianer, used to unhide.
    public virtual void onTakenOutOfContainer() {

        
        isWithinContainer = null;

        print("out of inv");

        showToWorld();

        setController(Controller.Local);
        sendNetworkData("OutInv," + Vector3ToString(transform.position) + "," + QuaternionToString(transform.rotation) +","+ Vector3ToString(rig.velocity) + "," + Vector3ToString(rig.angularVelocity));
    }

    //Used to change actions and rotate placing rotations
    public virtual void onScrollWheelScrollDown()
    {
        useIndex++;
        useIndex = Mathf.Clamp(useIndex, 0, Mathf.Max(primaryUses.Count - 1, secondaryUses.Count - 1));
        changePlaceRotation(11.25f);
    }
    //Used to change actions and rotate placing rotations
    public virtual void onScrollWheelScrollUp()
    {
        useIndex--;
        useIndex = Mathf.Clamp(useIndex, 0, Mathf.Max(primaryUses.Count - 1, secondaryUses.Count - 1));
        changePlaceRotation(-11.25f);
    }


    //This makes the item render in front of anything else 
    protected void setLayerToHands() {
        gameObject.layer = GameReference.self.handItem;
        foreach (Transform go in GetComponentInChildren<Transform>())
        {
            go.gameObject.layer = GameReference.self.handItem;
        }
    }

    //restores the item to render normally (not in front of everything else)
    protected void setLayerToWorld()
    {
        gameObject.layer = GameReference.self.defaultLayer;
        foreach (Transform go in GetComponentInChildren<Transform>())
        {
            go.gameObject.layer = GameReference.self.defaultLayer;
        }
    }

    //used to set hand positions, play audio and show the item to the world
    public virtual void onEnterHands() {

        GameReference.self.playerController.onHandsNowNotEmpty();

        canStartAction = true;

        showToWorld();

        if (itemAquireSound != null)
        {
        //    itemAquireSound.Play();
        }
        else {

            itemAquireSound = makeAudioSourceFromGenericSounds(GameReference.self.genericSounds.itemAquire);
        }

        setLayerToHands();

         rig = GetComponent<Rigidbody>();
        rig.isKinematic = false;

        if (anim != null) {
           
            anim.enabled = true;
          
        }
        CancelInvoke("getAndSetActionText");
        InvokeRepeating("getAndSetActionText", 0, 0.1f);


        isWithinContainer = null;   
        GameReference.self.playerIK.setLeftHand(getLeftHand());
        GameReference.self.playerIK.setRightHand(getRightHand());


        GameReference.self.handBobber.setMassMultiplier(rig.mass);

        stopSyncingUntilRest();

        sendNetworkData("InPlayerHand");
        

    }
    //checks if the item is within a container
    public bool isInContainer() {
        return isWithinContainer != null;
    }
    //returns the container (if any) the item is contained within
    public Container getisInContainer() {
        return isWithinContainer;
    }

    //forces the item to be dropped, disables animation, clears parent and container and enables physics
    public virtual void drop() {


        if (anim != null) {
            anim.enabled = false;
        }


         if (GameReference.self.playerWearables.HandItemObj == transform) {
            GameReference.self.playerWearables.HandItemObj = null;
            onExitHands(true);

        }
      
        transform.SetParent(null);
        transform.position = GameReference.self.mainCamera.transform.position + GameReference.self.mainCamera.transform.forward * 1.5f;


        rig.velocity = Vector3.zero;
      //  enableAllColliders();
        showToWorld();

        if (isWithinContainer != null)
        {
            isWithinContainer.removeByID(ID, getInventorySize()); ;
            GameReference.self.playerInventory.UpdateInventory();
            onTakenOutOfContainer();
        }
        else {
       //     print("ID NOT IN CONTAINER");
        }
        isWithinContainer = null;
        itemInHands = false;

        wakeUp();
    }

    //handles everything to do when the item is removed from the player's hands
    public virtual void onExitHands(bool isDropped) {


        setLayerToWorld();
        CancelInvoke("getAndSetActionText");

        GameReference.self.itemActionUIController.doesHaveUse = false;
        GameReference.self.itemActionUIController.checkForDoesHaves();


        GameReference.self.playerIK.setLeftHand(null);
        GameReference.self.playerIK.setRightHand(null);

        disableAnimation();
        if (isDropped)
        {
            sendNetworkData("Dropped," + Vector3ToString(transform.position, 4) + "," + QuaternionToString(transform.rotation, 4) + "," + Vector3ToString(rig.velocity, 4) + "," + Vector3ToString(rig.angularVelocity, 4));

        }
        else {
            sendNetworkData("OutPlayerHand," + Vector3ToString(transform.position, 4) + "," + QuaternionToString(transform.rotation, 4) + "," + Vector3ToString(rig.velocity, 4) + "," + Vector3ToString(rig.angularVelocity, 4));

        }
        SyncUntilRest();

        setController(Controller.Local);
        GameReference.self.playerController.onHandsNowEmpty();

    }

    //adds the list of available actions to the primary and secondary actions list
    public virtual void getActions()
    {

        bool doBase = true;//do base allows me to cancel basic actions
        if (this is Placeable)
        {
            if (((Placeable)this).currentPlaceState == Placeable.placeState.placing)
            {
                doBase = false;
            }
        }
        else if (this is Tether)
        {
            if (((Tether)this).currentTetherState != Tether.TetherState.NoneAttached)
            {
                doBase = false;
            }
        }
        else if (this is ElectronicController)
        {
            if (((ElectronicController)this).getIsUsing())
            {
                doBase = false;
            }
        }
        else if (this is Dodgeball)
        {

                doBase = false;
            
        }

        if (doBase)
        {
            //these are default basic actions
            secondaryUses.Add(new UserAction("Place", delegate { startPrecisionPlace(); }, null, null, ""));
            secondaryUses.Add(new UserAction("Throw", delegate { startThrow(); }, delegate { whileThrow(); }, delegate { throwObject(); }, ""));
        }


        if (GameReference.self.playerController.getItemLookingAt() != null) {
            if (GameReference.self.playerController.getItemLookingAt().GetComponent<Container>())
            {
                if (GameReference.self.playerController.getItemLookingAt().GetComponent<Container>().needsPart(this))
                {

                    primaryUses.Add(new UserAction("Install " + objectName + " In " + GameReference.self.playerController.getItemLookingAt().GetComponent<WorldItem>().getObjectName(), delegate { startInstall(GameReference.self.playerController.getItemLookingAt().GetComponent<Container>()); }, delegate { whileInstall(); }, delegate { endInstall(); }, ""));

                }
                else
                {

                }
            }else if (GameReference.self.playerController.getItemLookingAt().GetComponentInParent<Container>())
            {

                if (GameReference.self.playerController.getItemLookingAt().GetComponentInParent<Container>().needsPart(this))
                {

                    primaryUses.Add(new UserAction("Install " + objectName + " In " + GameReference.self.playerController.getItemLookingAt().GetComponentInParent<WorldItem>().getObjectName(), delegate { startInstall(GameReference.self.playerController.getItemLookingAt().GetComponentInParent<Container>()); }, delegate { whileInstall(); }, delegate { endInstall(); }, ""));

                }
                else
                {

                }
            }
        }
      
    }

    //starts installing one item in a container
    void startInstall(Container installContainer) {
        GameReference.self.actionProgress.genericStartCounting(1, 0);
        ActionProgress.event_Completed += delegate { makeInstall(installContainer); };
    }
    //just here if needed later
    void whileInstall() {

    }
    //finishes the install UI
    void endInstall() {
        GameReference.self.actionProgress.genericStopCounting();
  
    }

    //does the installing
    void makeInstall(Container installContainer) {
        print("INSTALLED");
        if (installContainer.needsPart(this)) {
            installContainer.addPartAuto(this);
            GameReference.self.playerWearables.HandItemObj = null;
            GameReference.self.playerWearables.HandItemUI = null;
            GameReference.self.playerWearables.OnChange();
            takeFromHands();
            onPutInContainer(installContainer);
        }
    }
    //starts throw UI
    void startThrow() {
        throwVelocity = 1;
        GameReference.self.actionProgress.genericStartCounting(2, 0);
    }
    //updates throw velocity based on how long action is held for
    void whileThrow() {
        throwVelocity = Mathf.Clamp(throwVelocity+Time.deltaTime*5,1,10);
        
    }
    //finally throws the item
    void throwObject()
    {
        GameReference.self.actionProgress.genericStopCounting();
        drop();
        rig.velocity = GameReference.self.playerController.rig.velocity;
        rig.AddForce(GameReference.self.mainCamera.transform.forward*throwVelocity, ForceMode.Impulse);
        rig.AddRelativeTorque(new Vector3(Random.Range(-3, 3), Random.Range(-3, 3), Random.Range(-3, 3)));
        GameReference.self.playerController.objectThrown(this, throwVelocity);
    }


    //retrieves and displays the current available actions for the player 
    public virtual void getAndSetActionText()
    {
        GameReference.self.itemActionUIController.setName(getObjectName());
        GameReference.self.itemActionUIController.setPrimaryUse("-");

        GameReference.self.itemActionUIController.setSecondayUse("-");

        GameReference.self.itemActionUIController.setConsumable(getObjectName(),condition,100, getSprite());

        primaryUses = new List<UserAction>();
        secondaryUses = new List<UserAction>();

        if (!isPrecisionPlacing)
        {
            getActions();
        }
        else {
            primaryUses.Add(new UserAction("Place " + getObjectName(), delegate { makePrecisionPlace(); }, null, null, "")); ;

          //  primaryUses.Add(new UserAction("Stick Item", delegate { makePrecisionStick(); }, null, null, ""));

            secondaryUses.Add(new UserAction("Cancel place", delegate { stopPrecisionPlace(); }, null, null, ""));
        }

        if (primaryUses.Count != 0)
        {
            GameReference.self.itemActionUIController.setPrimaryUse(primaryUses[Mathf.Clamp(useIndex, 0, primaryUses.Count - 1)].DisplayString);
            if (Mathf.Clamp(useIndex, 0, primaryUses.Count - 1) == primaryUses.Count - 1)
            {
                GameReference.self.itemActionUIController.setPrimaryScrollDown(false);
            }
            else
            {
                GameReference.self.itemActionUIController.setPrimaryScrollDown(true);
            }

            if (Mathf.Clamp(useIndex, 0, primaryUses.Count - 1) == 0)
            {
                GameReference.self.itemActionUIController.setPrimaryScrollUp(false);
            }
            else
            {
                GameReference.self.itemActionUIController.setPrimaryScrollUp(true);
            }


        }
        else {
            GameReference.self.itemActionUIController.setPrimaryScrollUp(false);
            GameReference.self.itemActionUIController.setPrimaryScrollDown(false);
        }
        if (secondaryUses.Count != 0)
        {
            GameReference.self.itemActionUIController.setSecondayUse(secondaryUses[Mathf.Clamp(useIndex, 0, secondaryUses.Count - 1)].DisplayString);

            if (Mathf.Clamp(useIndex, 0, secondaryUses.Count - 1) == secondaryUses.Count - 1)
            {
                GameReference.self.itemActionUIController.setSecondaryScrollDown(false);
            }
            else
            {
                GameReference.self.itemActionUIController.setSecondaryScrollDown(true);
            }

            if (Mathf.Clamp(useIndex, 0, secondaryUses.Count - 1) == 0)
            {
                GameReference.self.itemActionUIController.setSecondaryScrollUp(false);
            }
            else
            {
                GameReference.self.itemActionUIController.setSecondaryScrollUp(true);
            }

        }
        else {
            GameReference.self.itemActionUIController.setSecondaryScrollUp(false);
            GameReference.self.itemActionUIController.setSecondaryScrollDown(false);
        }

    
     

        GameReference.self.itemActionUIController.doesHaveUse = true;
        GameReference.self.itemActionUIController.checkForDoesHaves();

    }

    //called when the action can no longer proceed (eg if looked away while fixing something)
    public virtual void currentActionNoLongerAvailable()
    {
        if (currentAction != null)
        {
            canStartAction = true;
            currentAction.UserActionFunctionEnd?.Invoke();
            anim.SetBool(currentAction.AnimBool, false);
            currentAction = null;
        }
    }

    //checks the action is still valid, if it is not, call function above.
    public void actionValidationCheck()
    {
        if (currentAction != null)//IF I AM doing an action
        {
            bool isStillValid = false;
            if (isCurrentPrimaryAction)//and its primary
            {
                foreach (UserAction userAction in primaryUses)
                {
                    if (userAction.DisplayString == currentAction.DisplayString)
                    {
                        isStillValid = true;
                    }
                }
            }
            else
            {
                foreach (UserAction userAction in secondaryUses)
                {
                    if (userAction.DisplayString == currentAction.DisplayString)
                    {
                        isStillValid = true;
                    }
                }
            }
            if (!isStillValid)
            {
              //  print("NO LONGER VALID");
                currentActionNoLongerAvailable();
            }
        }
        else {
         //   print("Not performing action");
        }
    }

    //returns if the item is currently performing an action
    public bool isBeingUsed() {
        return !canStartAction;
    }

    //called when a primary use begins
    public virtual void onPrimaryUseStart()
    {

        if (primaryUses.Count != 0&& canStartAction)
        {
            canStartAction = false;
            int index = Mathf.Clamp(useIndex, 0, primaryUses.Count - 1);

            currentAction = primaryUses[index];
            isCurrentPrimaryAction = true;

            currentAction.UserActionFunctionStart?.Invoke();
            if (currentAction.AnimBool != "" && currentAction.AnimBool != null)
            {
                anim.SetBool(currentAction.AnimBool, true);
                sendNetworkData("Anim," + currentAction.AnimBool + "," + BoolToString(true));
            }
        }
    }



    //called while a primary use is being performed
    public virtual void whilePrimaryUse()
    {

        if (currentAction!=null&&isCurrentPrimaryAction)
        {
            currentAction.UserActionFunctionWhile?.Invoke();
        }
    }


    //called when a primary use ends or is cancelled
    public virtual void onPrimaryUseEnd()
    {
        if (currentAction != null && isCurrentPrimaryAction)
        {
            currentAction.UserActionFunctionEnd?.Invoke();

            if (currentAction.AnimBool != "" && currentAction.AnimBool != null)
            {
                anim.SetBool(currentAction.AnimBool, false);
                sendNetworkData("Anim," + currentAction.AnimBool+","+BoolToString(false));
            }

            canStartAction = true;

            currentAction = null;
        }
           
    }

    //called when a secondary use begins
    public virtual void onSecondaryUseStart()
    {


        
        if (secondaryUses.Count != 0&& canStartAction)
        {
            canStartAction = false;
            int index = Mathf.Clamp(useIndex, 0, secondaryUses.Count - 1);

            currentAction = secondaryUses[index];
            isCurrentPrimaryAction = false;

            currentAction.UserActionFunctionStart?.Invoke();
            if (currentAction.AnimBool != "" && currentAction.AnimBool != null)
            {
                anim.SetBool(currentAction.AnimBool, true);
                sendNetworkData("Anim," + currentAction.AnimBool + "," + BoolToString(true));
            }

        }

    }
    //called while a secondary use is being performed
    public virtual void whileSecondaryUse()
    {
        if (currentAction != null&&!isCurrentPrimaryAction)
        {
            currentAction.UserActionFunctionWhile?.Invoke();
        }
    }

    //called when a secondary use ends or is cancelled
    public virtual void onSecondaryUseEnd()
    {

        if (currentAction != null && !isCurrentPrimaryAction)
        {
            currentAction.UserActionFunctionEnd?.Invoke();

            if (currentAction.AnimBool != "" && currentAction.AnimBool != null)
            {
                anim.SetBool(currentAction.AnimBool, false);
                sendNetworkData("Anim," + currentAction.AnimBool + "," + BoolToString(false));
            }

            canStartAction = true;

            currentAction = null;
        }


    }
  
    //does the display of precision placing, used to have a lerp function but that is commented out for now, kept it for later
    public virtual void whileItemInHands() {
        if (isPrecisionPlacing)
        {
            RaycastHit hit;
            if (Physics.Raycast(GameReference.self.mainCamera.transform.position, GameReference.self.mainCamera.transform.forward, out hit, 6,GameReference.self.placingLayer))
            {
                transform.rotation = Quaternion.identity;
                transform.Rotate(-Vector3.Cross(transform.TransformDirection(rotationAxis), hit.normal), -Vector3.SignedAngle(transform.TransformDirection(rotationAxis), hit.normal, Vector3.Cross(transform.TransformDirection(rotationAxis), hit.normal)));
                transform.Rotate(rotationAxis, currentRotation);
                
                //Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.TransformDirection(Vector3.forward),hit.normal), hit.normal);
               // transform.Rotate(Vector3.Cross(transform.TransformDirection(upDirection), hit.normal), -Vector3.SignedAngle(transform.TransformDirection(upDirection), hit.normal, Vector3.Cross(transform.TransformDirection(upDirection), hit.normal)));
                transform.position = hit.point + (transform.TransformDirection(rotationAxis) * offset);

            }
        }
        //Keeping this as I may need it later, the code smoothly lerps the item from the hand position to the world position
                /*
                  if (isPrecisionPlacing)
                  {
                      disableAllColliders();
                  }
                  else
                  {
                      enableAllColliders();
                  }
                  // GetComponent<Collider>().enabled = !isPrecisionPlacing;
                  if (!isPrecisionPlacing)
                  {
                      //putInFirstPersonHands();

                  }
                  else
                  {

                      takeFromFirstPersonHands();
                  }*/
           // }
        /*
        if (isPrecisionPlacing)
        {
            if (anim != null)
            {
                anim.SetBool("PrimaryUse", false);
                anim.enabled = false;


            }
            RaycastHit hit;
            if (Physics.Raycast(GameReference.self.mainCamera.transform.position, GameReference.self.mainCamera.transform.forward, out hit, 6))
            {
                if (!IsLerping)
                {
                    LerpVectorTo = hit.point;




                    transform.position = hit.point + transform.TransformDirection(upDirection * offset);//+ transform.TransformDirection(vectorOffset);

                    //  transform.rotation = Quaternion.Euler(hit.normal);
                    //transform.rotation = Quaternion.LookRotation(transform.TransformDirection(Vector3.forward), hit.normal);

                    transform.up = hit.normal;

                    float Angle = Vector3.SignedAngle(hit.normal, transform.TransformDirection(upDirection), Vector3.Cross(hit.normal, transform.TransformDirection(upDirection)));
                    //                            print(Angle);
                    Debug.DrawLine(transform.position, transform.position + hit.normal, Color.red, 2);
                    Debug.DrawLine(transform.position, transform.position + transform.TransformDirection(upDirection), Color.green, 2);
                    Debug.DrawLine(transform.position, transform.position + Vector3.Cross(hit.normal, transform.TransformDirection(upDirection)), Color.blue, 2);


                    transform.Rotate(Vector3.Cross(hit.normal, transform.TransformDirection(upDirection)), Angle, Space.World);
                    Angle = 180 - Vector3.SignedAngle(hit.normal, transform.TransformDirection(upDirection), Vector3.Cross(hit.normal, transform.TransformDirection(upDirection)));
                    //                            print("b"+ Angle);
                    //transform.Rotate(transform.TransformDirection(rotationAxis), precisionPlaceRotation);
                    transform.RotateAround(transform.position + transform.TransformDirection((upDirection * offset)), transform.TransformDirection(upDirection), precisionPlaceRotation);
                }
                else
                {
                    LerpVectorTo = hit.point + transform.TransformDirection((upDirection * offset));
                    LerpRotationTo = Quaternion.LookRotation(transform.TransformDirection(Vector3.forward), hit.normal);
                }

            }
           

           
        }
        else
        {
            //   transform.position = GameReference.self.mainCamera.transform.position + GameReference.self.mainCamera.transform.forward * 2 - GameReference.self.mainCamera.transform.up * 0.5f;
        }*/
    }

 
    //makes the UI item, sets the sprite and size/dimentions
    public GameObject makeUIItem() {
        GameReference.self.playerInventory = GameObject.FindGameObjectWithTag("Canvas").GetComponent<PlayerInventory>();
        GameObject tempItem = Instantiate(GameReference.self.playerInventory.vicinityItem, GameReference.self.playerInventory.vicinitySlotHolder.position, Quaternion.identity, GameReference.self.playerInventory.vicinitySlotHolder);//if error, base.Start on new obj
       // GameObject tempItem = Instantiate(playerInventory.vicinityItem, playerInventory.vicinitySlotHolder.position, Quaternion.identity, playerInventory.vicinitySlotHolder);
        tempItem.transform.GetComponent<InventoryItem>().setSprite(getSprite());
        tempItem.GetComponent<InventoryItem>().setSize(getInventorySize());
        tempItem.transform.GetComponent<RectTransform>().sizeDelta = tempItem.transform.GetComponent<RectTransform>().sizeDelta;
        tempItem.GetComponent<InventoryItem>().setObjectRef(gameObject);
        return tempItem;
    }

    //gets the left hand position's transform
    public Transform getLeftHand()
    {
        if (leftHandAnim == null) {
            return null;
        }
        return leftHandAnim.transform;
    }

    //gets the right hand position's transform
    public Transform getRightHand()
    {
        if (rightHandAnim == null)
        {
            return null;
        }
        return rightHandAnim.transform;
    }

  

    public virtual void putInHands()
    {
        onEnterHands();
       
       
        transform.parent = GameReference.self.playerController.getFirstPersonHand();
        transform.localPosition= Vector3.zero;
        transform.localRotation = Quaternion.identity;

        rig.isKinematic = true;

        disableAllColliders();

        setController(Controller.Local);

        itemInHands = true;
    }
  
    //TODO: needs to be removed, but other functions depend on it, do later
    public virtual void getOptions()
    {
        if (isInHands())
        {
          //  GameReference.self.scrollSelect.addOption(ScrollSelect.ScrollOptionType.Drop, dropFromHands);
         //   GameReference.self.scrollSelect.addOption(ScrollSelect.ScrollOptionType.PutInInv, StoreInInventory);
         //   GameReference.self.scrollSelect.addOption(ScrollSelect.ScrollOptionType.Place, startPrecisionPlace);
            //   radialMenu.addOption("Drop").GetComponent<Button>().onClick.AddListener(dropFromHands);
            // radialMenu.addOption("Put in Inv").GetComponent<Button>().onClick.AddListener(StoreInInventory);
            // radialMenu.addOption("Place").GetComponent<Button>().onClick.AddListener(startPrecisionPlace);
        }
    }




    //Much like drop from hands, but for installing the item, (the item never drops but gets transfered)
    public void takeFromHands()
    {
        onExitHands(false);
        transform.parent = null;
        if (actionsTransform != null)
        {
            GameObject.Destroy(actionsTransform.gameObject);
        }
        itemInHands = false;
    }

    //removes the item from the player's hand
    public void removeFromInventory() {
        if (itemInHands)
        {
            itemInHands = false;
            GameReference.self.playerInventory.destroyUIHandItem();
            GameReference.self.playerWearables.dropFromHands();
        }
        rig = GetComponent<Rigidbody>();
        rig.isKinematic = false;

        
        transform.parent = null;
        enableAllColliders();
        rig.velocity = GameReference.self.playerController.rig.velocity;
    }

    //drops the item from the player's hands, enables physics
    public void dropFromHands()
    {
        onExitHands(true);
      
        //takeFromFirstPersonHands();
        if (actionsTransform != null)
        {
            GameObject.Destroy(actionsTransform.gameObject);
        }

        GameReference.self.playerInventory.destroyUIHandItem();
        GameReference.self.playerWearables.dropFromHands();

        itemInHands = false;

        rig.isKinematic = false;


        transform.parent = null;
        enableAllColliders();
        rig.velocity = GameReference.self.playerController.rig.velocity;
        afterDroppedFromHands();

        SyncUntilRest();

    }

    //used in classes which inherit from this
    protected virtual void afterDroppedFromHands() {

    }
    //hides the item, as if it never existed
    protected void hideFromWorld() {
        isHiddenFromWorld = true;
        disableAllColliders();
        disableAllRenderers();
        if (rig == null)
        {
            rig = GetComponent<Rigidbody>();
        }
        rig.isKinematic = true;
    }

    //shows the item to the world
    protected void showToWorld() {
        isHiddenFromWorld = false; 
        enableAllColliders();
        enableAllRenderers();
       
    }

    //used for inititalisation of items
    public void forceSetIsHiddenFromWorld(bool val) {
        isHiddenFromWorld = val;
    }



    //returns if is in hands
    public bool isInHands()
    {
        return itemInHands;
    }
 
    //gets thr sprite for the item
    public Sprite getSprite()
    {
        return itemSprite;
    }

   //disables all colliders
    public void disableAllColliders()
    {
        foreach (Collider collider in GetComponents<Collider>())
        {
            collider.enabled = false;
        }
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            if (collider.transform.GetComponentsInParent<PickUpable>().Length <= 1) { 
                collider.enabled = false;
            }
        }
    }

    //enables all colliders
    public void enableAllColliders()
    {
/*        if (this is Drill) {
            Debug.LogError("asd");
  */      
        foreach (Collider collider in GetComponents<Collider>())
        {
            collider.enabled = true;
        }
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            if (collider.GetComponentsInParent<PickUpable>().Length<=1)
            {//stop activating other items

                collider.enabled = true;
            }
        }
    }

    //disables all renderers
    public void disableAllRenderers()
    {
        foreach (MeshRenderer meshRenderer in GetComponents<MeshRenderer>())
        {
            meshRenderer.enabled = false;
        }
        foreach (MeshRenderer meshRenderer in GetComponentsInChildren<MeshRenderer>())
        {
            meshRenderer.enabled = false;
        }
        foreach (SkinnedMeshRenderer meshRenderer in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            meshRenderer.enabled = false;
        }
    }

    //enables all renderers
    public void enableAllRenderers()
    {
        
        foreach (MeshRenderer meshRenderer in GetComponents<MeshRenderer>())
        {
            meshRenderer.enabled = true;
        }
        foreach (MeshRenderer meshRenderer in GetComponentsInChildren<MeshRenderer>())
        {
            meshRenderer.enabled = true;
        }
        foreach (SkinnedMeshRenderer meshRenderer in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            meshRenderer.enabled = true;
        }
    }

    //This gets stats about the item to display to the user
    public override string getToString()
    {
       
        string stringToReturn = base.getToString();
        stringToReturn += "\n Inv Space:\t" + inventorySize.x + "x" + inventorySize.y;
        return stringToReturn;
    }

    //network sync for entering hands
    public virtual void onEnterHandsNetwork(PlayerNetworkIndividualController playerNetworkIndividualController)
    {

        setController(Controller.Local);

        showToWorld();
        disableAllColliders();

        itemInNetworkHands = true;
  
        if (itemAquireSound != null)
        {
         //   itemAquireSound.Play();
        }
        else
        {

            itemAquireSound = makeAudioSourceFromGenericSounds(GameReference.self.genericSounds.itemAquire);
        }

        

        rig = GetComponent<Rigidbody>();
        rig.isKinematic = false;


        if (anim != null)
        {
            anim.enabled = true;
        }
        isWithinContainer = null;


        stopSyncingUntilRest();

        playerNetworkIndividualController.playerIK.setLeftHand(getLeftHand());
        playerNetworkIndividualController.playerIK.setRightHand(getRightHand());
        transform.parent = playerNetworkIndividualController.getHand();
        playerNetworkIndividualController.setHandObject(this);

        setAbleToPickup(false);


    }

    //network sync for exiting hands
    public virtual void onExitHandsNetwork(PlayerNetworkIndividualController playerNetworkIndividualController,bool dropped)
    {

        setController(Controller.Network);

        setLayerToWorld();

        itemInNetworkHands = false;

        disableAnimation();
        playerNetworkIndividualController.playerIK.resetHands();
        playerNetworkIndividualController.setHandObject(null);

        if (dropped)
        {
            showToWorld();
        }
        setAbleToPickup(true);

    }

    //network sync for put in container
    public virtual void onPutInContainerNetwork(Container c,PlayerNetworkIndividualController playerNetworkIndividualController)
    {

        setController(Controller.Network);

        if (itemInInv != null)
        {
    //        itemInInv.Play();
        }
        isWithinContainer = c;
     
        if (itemInNetworkHands)
        {
            onExitHandsNetwork(playerNetworkIndividualController,false);
        }

        stopSyncingUntilRest();
        hideFromWorld();


        setAbleToPickup(true);
    }

    //network sync for taken out of container
    public virtual void onTakenOutOfContainerNetwork()
    {
        isWithinContainer = null;

        setController(Controller.Network);

        //       sendNetworkData("OutInv," +Vector3ToString(transform.position)+","+QuaternionToString(transform.rotation));

        showToWorld();

        
        setAbleToPickup(true);

    }



    //this is used to process the incomming data from the item
    public override void recieveNetworkData(CSteamID steamID, string[] dataSplit)
    {

        PlayerNetworkIndividualController playerNetworkIndividualController = GameReference.self.playerNetwork.getIndividualController(steamID);
        base.recieveNetworkData(steamID, dataSplit);
        if (dataSplit[1] == "InPlayerHand")
        {


            onEnterHandsNetwork(playerNetworkIndividualController);

        }
        else if (dataSplit[1] == "OutPlayerHand")
        {

            //print(rig.velocity);
            onExitHandsNetwork(playerNetworkIndividualController, false);
            transform.parent = null;
            transform.position = new Vector3(float.Parse(dataSplit[2]), float.Parse(dataSplit[3]), float.Parse(dataSplit[4]));
            transform.rotation = new Quaternion(float.Parse(dataSplit[5]), float.Parse(dataSplit[6]), float.Parse(dataSplit[7]), float.Parse(dataSplit[8]));

            rig.velocity = new Vector3(float.Parse(dataSplit[9]), float.Parse(dataSplit[10]), float.Parse(dataSplit[11]));
            rig.angularVelocity = new Vector3(float.Parse(dataSplit[12]), float.Parse(dataSplit[13]), float.Parse(dataSplit[14]));

            rig.isKinematic = false;
        }
        else if (dataSplit[1] == "Dropped")
        {
            onExitHandsNetwork(playerNetworkIndividualController, true);
            transform.parent = null;
            transform.position = new Vector3(float.Parse(dataSplit[2]), float.Parse(dataSplit[3]), float.Parse(dataSplit[4]));
            transform.rotation = new Quaternion(float.Parse(dataSplit[5]), float.Parse(dataSplit[6]), float.Parse(dataSplit[7]), float.Parse(dataSplit[8]));

            rig.velocity = new Vector3(float.Parse(dataSplit[9]), float.Parse(dataSplit[10]), float.Parse(dataSplit[11]));
            rig.angularVelocity = new Vector3(float.Parse(dataSplit[12]), float.Parse(dataSplit[13]), float.Parse(dataSplit[14]));

            rig.isKinematic = false;
        }
        else if (dataSplit[1] == "InInv")
        {

            onPutInContainerNetwork(Savable.findGameObjectByID((int)StringToFloat(dataSplit[2])).GetComponent<Container>(), playerNetworkIndividualController);
        }
        else if (dataSplit[1] == "OutInv")
        {
            onTakenOutOfContainerNetwork();
            transform.position = new Vector3(float.Parse(dataSplit[2]), float.Parse(dataSplit[3]), float.Parse(dataSplit[4]));
            transform.rotation = new Quaternion(float.Parse(dataSplit[5]), float.Parse(dataSplit[6]), float.Parse(dataSplit[7]), float.Parse(dataSplit[8]));

            rig.velocity = new Vector3(float.Parse(dataSplit[9]), float.Parse(dataSplit[10]), float.Parse(dataSplit[11]));
            rig.angularVelocity = new Vector3(float.Parse(dataSplit[12]), float.Parse(dataSplit[13]), float.Parse(dataSplit[14]));

        }
        else if (dataSplit[1] == "Anim")
        {
            anim.SetBool(dataSplit[2], StringToBool(dataSplit[3]));
        }
        else if (dataSplit[1] == "InvRot") {
            rotatedItem = StringToBool(dataSplit[2]);
        }

    }

    //gets the item's attributes for saving to a text file
    public override List<Attribute> getAttributes()
    {
        List<Attribute> attrs =   base.getAttributes();
        if (isHiddenFromWorld) {
            attrs.Add(new Attribute("Hidden", "<3"));
        }
        attrs.Add(new Attribute("containerIn", isInContainer() ? FloatToString(isWithinContainer.GetComponent<Savable>().getID() ) : "-1"));
        attrs.Add(new Attribute("InvRot", BoolToString(rotatedItem)));
        return attrs;
    }

    //sets the item's attributes once loaded from a text file
    public override void setAttributes(List<Attribute> attributes)
    {
        base.setAttributes(attributes);

        bool testHidden = false;
        foreach (Attribute attr in attributes) {
            if (attr.Key == "InvRot")
            {
                rotatedItem = StringToBool(attr.Value);
                //if (rotatedItem) {
              //  Debug.Break();
                // }
            }
            if (attr.Key == "Hidden") {
                testHidden = true;
            }
            if (attr.Key == "containerIn")
            {
                if (attr.Value != "-1")
                {
//                    Debug.Log(attr.Value, gameObject);
                    isWithinContainer = Savable.findGameObjectByID((int)StringToFloat(attr.Value)).GetComponent<Container>();

                }
            }
         
        }

        if (testHidden) {
            hideFromWorld();
        }
        else {
            showToWorld();
        }


        
    }


    //helps set the offest and rotation axis in editor
    public override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = new Color(1, 1, 0, 1f);
        Gizmos.DrawSphere(transform.TransformPoint(-transform.TransformDirection(rotationAxis)* offset), 0.1f);
        Gizmos.color = new Color(0, 0, 1, 1f);
        Gizmos.DrawLine(transform.position, transform.position + transform.TransformDirection(rotationAxis));


        /*   Gizmos.DrawSphere(transform.position + transform.TransformDirection(leftHand.position), 0.1f);
               Gizmos.DrawLine(transform.position + transform.TransformDirection(leftHandPosition), transform.position + transform.TransformDirection(leftHandPosition) + transform.TransformDirection(leftHandRotation));
               Gizmos.color = new Color(0,1, 0, 1f);
               Gizmos.DrawSphere(transform.position + transform.TransformDirection(rightHandPosition), 0.1f);
               Gizmos.DrawLine(transform.position + transform.TransformDirection(rightHandPosition), transform.position + transform.TransformDirection(rightHandPosition) + transform.TransformDirection(rightHandRotation));
               */
    }
}

//custom user action class for primary/secondary action
public class UserAction {



    public string DisplayString;
    public delegate void FunctionToCall(); // This defines what type of method you're going to call.
    public FunctionToCall UserActionFunctionStart;
    public FunctionToCall UserActionFunctionWhile;
    public FunctionToCall UserActionFunctionEnd;
    public string AnimBool;

    public UserAction(string _DisplayString, FunctionToCall _UserActionFunctionStart, FunctionToCall _UserActionFunctionWhile, FunctionToCall _UserActionFunctionEnd,string _AnimBool) {
       
        DisplayString = _DisplayString;
        UserActionFunctionStart = _UserActionFunctionStart;
        UserActionFunctionWhile = _UserActionFunctionWhile;
        UserActionFunctionEnd = _UserActionFunctionEnd;
        AnimBool = _AnimBool;
    }
}
