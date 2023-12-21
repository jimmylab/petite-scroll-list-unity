using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

[RequireComponent(typeof(UIDocument))]
public class PetiteScrollListExample : MonoBehaviour {
    UIDocument doc;
    VisualElement root;

    TextField msgInput;
    Button btnSend;
    Button btnGenerate;
    Button btnClear;
    PetiteScrollList scrollList;
    IntegerField number;
    Button btnListItem;

    void OnEnable() {
        doc = GetComponent<UIDocument>();
        root = doc.rootVisualElement;

        msgInput    = root.Q<TextField>("message");
        btnSend     = root.Q<Button>   ("btn-send");
        number      = root.Q<IntegerField>("number");
        btnGenerate = root.Q<Button>   ("btn-generate");
        btnClear    = root.Q<Button>   ("btn-clear");
        scrollList  = root.Q<PetiteScrollList>();
        btnListItem = root.Q<Button>   ("btn-list-item");

        btnSend.clicked += () => addMessage(msgInput.value);
        btnGenerate.clicked += () => generateLines(number.value);
        btnClear.clicked += () => scrollList.Clear();

        btnListItem.clicked += () => {
            Debug.Log("clicked!");
        };
        var label = root.Q<Label>("clickable-label");
        label.RegisterCallback<PointerUpEvent>(ev => {
            Debug.Log("PointerUp!");
        });
        label.RegisterCallback<ClickEvent>(ev => {
            Debug.Log("Click!");
        });
    }

    void Update() { }

    public void addMessage(string msg) {
        scrollList.AddRow(new Label(msg));
        scrollList.ScrollToBottom();
    }
    public void generateLines(int N) {
        for (int i = 0; i < N; i++) {
            var label = new Label(
                $"<color=#c8c864>Message <color=red>{i+1}</color></color>: <color=#fff>{Lorem.Sentence(4)}</color>"
            );
            scrollList.AddRow(label);
        }
        scrollList.ScrollToBottom();
        Debug.Log($"{N} lines generated.");
    }
}
