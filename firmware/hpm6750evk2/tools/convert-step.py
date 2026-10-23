"""Local STEP -> GLB export; requires cadquery-ocp==8.0.1.0.0 (Python 3.14).

Preserve XCAF assembly instances and materials. Never overwrite a source/output.
Run prepare-model.mjs afterward before placing geometry in the host application.
"""
import sys
from pathlib import Path
from OCP.STEPCAFControl import STEPCAFControl_Reader
from OCP.TDocStd import TDocStd_Document
from OCP.TCollection import TCollection_ExtendedString, TCollection_AsciiString
from OCP.OCP.collections import Sequence_TDF_Label
from OCP.XCAFDoc import XCAFDoc_DocumentTool
from OCP.BRepMesh import BRepMesh_IncrementalMesh
from OCP.RWGltf import RWGltf_CafWriter
from OCP.OCP.collections import IndexedDataMap_TCollection_AsciiString_TCollection_AsciiString
from OCP.Message import Message_ProgressRange
from OCP.IFSelect import IFSelect_RetDone
from OCP.Interface import Interface_Static

source, target = sys.argv[1:]
if Path(target).exists():
    raise RuntimeError('Refusing existing output')
doc = TDocStd_Document(TCollection_ExtendedString('MDTV-XCAF'))
reader = STEPCAFControl_Reader()
reader.SetColorMode(True)
reader.SetNameMode(True)
Interface_Static.SetCVal_s('read.step.codepage', 'GB')
print('Reading assembly', flush=True)
if reader.ReadFile(source) != IFSelect_RetDone:
    raise RuntimeError('STEP read failed')
if not reader.Transfer(doc):
    raise RuntimeError('STEP transfer failed')
shape_tool = XCAFDoc_DocumentTool.ShapeTool_s(doc.Main())
roots = Sequence_TDF_Label()
shape_tool.GetFreeShapes(roots)
print('Tessellating roots', roots.Length(), flush=True)
for index in range(1, roots.Length()+1):
    shape = shape_tool.GetShape_s(roots.Value(index))
    mesher = BRepMesh_IncrementalMesh(shape, 0.5, False, 0.35, True)
    if not mesher.IsDone():
        raise RuntimeError('Tessellation failed')
writer = RWGltf_CafWriter(TCollection_AsciiString(target), True)
writer.SetMergeFaces(True)
writer.SetSplitIndices16(True)
writer.SetParallel(True)
if not writer.Perform(doc, IndexedDataMap_TCollection_AsciiString_TCollection_AsciiString(), Message_ProgressRange()):
    raise RuntimeError('GLB export failed')
print('Exported bytes', Path(target).stat().st_size, flush=True)
