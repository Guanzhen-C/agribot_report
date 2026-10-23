using UnityEngine;
using UnityEngine.UI;

public class slotController : MonoBehaviour
{
    private slotBtn currentSlot;

   
    public void SelectSlot(slotBtn newSlot)
    {
        if (currentSlot != null && currentSlot!=newSlot)  
        {
            currentSlot.SetSelected(false);

        }
        newSlot.SetSelected(true);
        currentSlot = newSlot;

    }


}                       
