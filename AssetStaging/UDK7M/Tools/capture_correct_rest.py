from pathlib import Path
R=Path('D:/UNITY工程/棍木项目/棍木项目/AssetStaging/UDK7M')
text=(R/'Tools/convert_animation.py').read_text(encoding='utf-8');exec(compile(text[:text.index('cache={};descriptions=[]')],str(R/'Tools/convert_animation.py'),'exec'))
(R/'correct_rest.json').write_text(json.dumps({n:list(map(list,m)) for n,m in rest_bones.items()}),encoding='utf-8')
action=bpy.data.actions['UDK7M_Idle'];assign(action);s.frame_set(0);bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get()
expected={}
for label,objects in [('Hull',rigids),('Tracks',tracks),('Hoses',hoses)]:
 expected['UDK7M_'+label]=np.concatenate([evaluated(o,dg)[::30] for o in objects]).tolist()
(R/'source_skin_points.json').write_text(json.dumps(expected),encoding='utf-8');print('CORRECT_REST_CAPTURED',len(rest_bones))
