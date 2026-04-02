from pandac.PandaModules import Point3, VBase3

objectStruct = {
    'Objects': {
        'sample-zone-root': {
            'Type': 'Street',
            'Name': 'Sample Zone Root',
            'Pos': Point3(0.0, 0.0, 0.0),
            'Hpr': VBase3(0.0, 0.0, 0.0),
            'Scale': VBase3(1.0, 1.0, 1.0),
            'Visual': {
                'Model': 'phase_4/models/neighborhoods/sz_grounds' },
            'Objects': {
                'sample-prop-01': {
                    'Name': 'Mailbox',
                    'Pos': Point3(4.5, 2.0, 0.0),
                    'Hpr': VBase3(90.0, 0.0, 0.0),
                    'Scale': VBase3(1.0, 1.0, 1.0),
                    'Visual': {
                        'Model': 'phase_4/models/props/mailbox' } },
                'sample-prop-02': {
                    'Name': 'Bench',
                    'Pos': Point3(-3.0, 1.0, 0.0),
                    'Hpr': VBase3(180.0, 0.0, 0.0),
                    'Scale': VBase3(1.0, 1.0, 1.0),
                    'Visual': {
                        'Model': 'phase_4/models/props/park_bench' } } } } } }
