"""Generate entirely artificial portal-interaction trials with the original 72-column schema.

NO source participant rows, participant-level estimates, or fitted generative
parameters are used. Values are independently simulated from illustrative rules.
This is a runnable ML-methodology demo, not a research dataset simulator.
"""
from pathlib import Path
import csv
import math
import numpy as np
import pandas as pd

SEED = 2026
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'data' / 'synthetic_portal_trials.csv'
# Original column ordering; schema only (not original observations).
COLUMNS = '''participant attempt_id phase start_time end_time choice_index success latency_s total_looks total_portal_gaze_s gaze_entropy num_unique_portals looks_portal_0 looks_portal_1 looks_portal_2 dwell_portal_0_s dwell_portal_1_s dwell_portal_2_s reentry_portal_0 reentry_portal_1 reentry_portal_2 total_reentries is_story_mode valid_choice valid_success valid_latency is_valid_trial n_available_portals max_entropy_possible normalized_entropy max_look_count max_dwell_s max_look_prop max_dwell_prop chosen_look_count chosen_dwell_s chosen_reentry_count chosen_look_prop chosen_dwell_prop chosen_reentry_prop chosen_is_most_dwelled chosen_is_most_viewed mean_other_dwell_s max_other_dwell_s mean_other_look_count max_other_look_count chosen_minus_mean_other_dwell_s chosen_minus_max_other_dwell_s chosen_minus_mean_other_looks chosen_minus_max_other_looks search_cost efficiency_success_only decision_efficiency decision_efficiency_cost_adjusted log_latency_s broad_exploration z_normalized_entropy z_num_unique_portals z_total_reentries z_total_looks z_max_dwell_prop z_chosen_dwell_prop exploration_index commitment_index sum_looks_portals sum_dwell_portals_s sum_reentry_portals looks_sum_matches dwell_sum_matches reentry_sum_matches chosen_dwell_rank_is_1 chosen_look_rank_is_1'''.split()

def main():
    rng = np.random.default_rng(SEED)
    records = []
    base_time = pd.Timestamp('2026-01-14 09:00:00')  # fictional date
    for pid in range(1, 25):
        participant = f'S{pid:02d}'  # plainly synthetic participant IDs
        phases = ['AssociationTraining'] * 9 + ['ThreePortalPractice'] * 2 + ['StoryMode'] * 5
        rng.shuffle(phases)
        for trial, phase in enumerate(phases, start=1):
            n = 3
            story = phase == 'StoryMode'
            # Weak, overlapping tendencies deliberately produce imperfect predictions.
            engagement = float(rng.normal(0, 0.5))
            latency = float(np.clip(rng.lognormal(3.25 - 0.12*engagement, .65), 5, 170))
            total_looks = int(np.clip(rng.poisson(12 + 1.5*max(0, engagement)) + 2, 2, 65))
            focus = rng.dirichlet(np.array([1.4, 1.4, 1.4]) * rng.uniform(.65, 1.5))
            looks = rng.multinomial(total_looks, focus)
            gaze_total = float(np.clip(latency * rng.uniform(.15, .68), .7, latency * .92))
            dwell = rng.dirichlet(np.maximum(looks, 0) + .4) * gaze_total
            reentries = np.maximum(looks - (looks > 0).astype(int), 0)
            choice = int(rng.choice(3, p=.65*(dwell / dwell.sum()) + .35/3))
            prob = .62 + (.09 if story else 0) + .035*engagement + .025*(choice==int(np.argmax(dwell)))
            success = int(rng.random() < np.clip(prob, .2, .88))
            start = base_time + pd.Timedelta(days=pid*2, minutes=trial*5)
            end = start + pd.Timedelta(seconds=latency)
            probs = looks / total_looks
            entropy = float(-sum(p*math.log(p) for p in probs if p > 0))
            other = [x for x in range(3) if x != choice]
            search_cost = int(total_looks + reentries.sum())
            chosen_look = int(looks[choice]); chosen_dwell = float(dwell[choice]); chosen_reentry = int(reentries[choice])
            rec = dict(participant=participant,attempt_id=trial,phase=phase,start_time=start.strftime('%Y-%m-%d %H:%M:%S.%f')[:-3],end_time=end.strftime('%Y-%m-%d %H:%M:%S.%f')[:-3],choice_index=choice,success=success,latency_s=latency,total_looks=total_looks,total_portal_gaze_s=gaze_total,gaze_entropy=entropy,num_unique_portals=int(sum(looks>0)),total_reentries=int(reentries.sum()),is_story_mode=story,valid_choice=True,valid_success=True,valid_latency=True,is_valid_trial=True,n_available_portals=n,max_entropy_possible=math.log(n),normalized_entropy=entropy/math.log(n),max_look_count=int(looks.max()),max_dwell_s=float(dwell.max()),max_look_prop=float(looks.max()/total_looks),max_dwell_prop=float(dwell.max()/gaze_total),chosen_look_count=chosen_look,chosen_dwell_s=chosen_dwell,chosen_reentry_count=chosen_reentry,chosen_look_prop=chosen_look/total_looks,chosen_dwell_prop=chosen_dwell/gaze_total,chosen_reentry_prop=chosen_reentry/max(int(reentries.sum()),1),chosen_is_most_dwelled=int(choice==np.argmax(dwell)),chosen_is_most_viewed=int(choice==np.argmax(looks)),mean_other_dwell_s=float(np.mean(dwell[other])),max_other_dwell_s=float(np.max(dwell[other])),mean_other_look_count=float(np.mean(looks[other])),max_other_look_count=int(np.max(looks[other])),chosen_minus_mean_other_dwell_s=chosen_dwell-float(np.mean(dwell[other])),chosen_minus_max_other_dwell_s=chosen_dwell-float(np.max(dwell[other])),chosen_minus_mean_other_looks=chosen_look-float(np.mean(looks[other])),chosen_minus_max_other_looks=chosen_look-int(np.max(looks[other])),search_cost=search_cost,efficiency_success_only=(success/latency) if success else np.nan,decision_efficiency=success/latency,decision_efficiency_cost_adjusted=success/(latency*(1+.02*search_cost)),log_latency_s=math.log(latency),broad_exploration=int(sum(looks>0)),sum_looks_portals=int(looks.sum()),sum_dwell_portals_s=float(dwell.sum()),sum_reentry_portals=int(reentries.sum()),looks_sum_matches=True,dwell_sum_matches=True,reentry_sum_matches=True,chosen_dwell_rank_is_1=int(choice==np.argmax(dwell)),chosen_look_rank_is_1=int(choice==np.argmax(looks)))
            for i in range(3):
                rec[f'looks_portal_{i}']=int(looks[i]);rec[f'dwell_portal_{i}_s']=float(dwell[i]);rec[f'reentry_portal_{i}']=int(reentries[i])
            records.append(rec)
    df = pd.DataFrame(records)
    # Synthetic within-cohort standardized summaries, not estimates from real data.
    for col in ['normalized_entropy','num_unique_portals','total_reentries','total_looks','max_dwell_prop','chosen_dwell_prop']:
        df['z_'+col] = (df[col]-df[col].mean())/(df[col].std(ddof=0) or 1)
    df['exploration_index'] = df[['z_normalized_entropy','z_num_unique_portals','z_total_reentries','z_total_looks']].sum(axis=1)
    df['commitment_index'] = df['z_max_dwell_prop']+df['z_chosen_dwell_prop']
    assert set(df.columns)==set(COLUMNS), (set(COLUMNS)-set(df.columns),set(df.columns)-set(COLUMNS))
    assert (df['total_looks']==df[[f'looks_portal_{i}' for i in range(3)]].sum(axis=1)).all()
    assert (df['total_portal_gaze_s'] <= df['latency_s']).all()
    OUT.parent.mkdir(parents=True,exist_ok=True)
    df[COLUMNS].to_csv(OUT,index=False,float_format='%.6f')
    print(f'Generated {len(df)} fully synthetic records, {len(COLUMNS)} fields: {OUT}')
if __name__=='__main__': main()
