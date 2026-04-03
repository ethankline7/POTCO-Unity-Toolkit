from pandac.PandaModules import Point3, VBase3

objectStruct = {}
objectStruct["Objects"] = {}

objectStruct["Objects"]["assign-root"] = {
    "Name": "Assignment Root",
    "Pos": Point3(0.0, 0.0, 0.0),
    "Hpr": VBase3(0.0, 0.0, 0.0),
    "Scale": VBase3(1.0, 1.0, 1.0),
    "Visual": {
        "Model": "phase_4/models/neighborhoods/sz_grounds"
    },
    "Objects": {}
}

objectStruct["Objects"]["assign-root"]["Objects"]["assign-prop"] = {
    "Name": "Assignment Mailbox",
    "Pos": Point3(
        2.0,
        3.0,
        0.0
    ),
    "Hpr": VBase3(90.0, 0.0, 0.0),
    "Visual": {
        "Model": "phase_4/models/props/mailbox"
    }
}

objectStruct["Objects"]["assign-root"]["Objects"]["assign-prop"]["Zone"] = "TutorialPlayground"
objectStruct["Objects"]["assign-root"]["Objects"]["assign-prop"]["DNA"] = "tt_mbox_default"
