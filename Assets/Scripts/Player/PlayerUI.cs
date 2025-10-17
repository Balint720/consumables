using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UIElements;

using Image = UnityEngine.UI.Image;

public partial class PlayerControl : EntityClass
{
    GameObject canvasGO;
    Canvas playerCanvas;
    CanvasRenderer playerCanvasRenderer;
    Image crossHair;
}