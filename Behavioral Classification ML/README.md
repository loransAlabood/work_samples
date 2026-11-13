# Behavioral ML: a methods demonstration on synthetic data

A research-style machine-learning workflow for **repeated-measures behavioral data**, built to show *how* I validate a model, not to report a result.

> **Data and results disclaimer.** The dataset in `data/` is **entirely artificial**, generated from hand-chosen illustrative distributions. It contains no original participant records and mirrors only the column structure of a confidential research dataset. Counts, labels, distributions and relationships are synthetic. Nothing in this repository is a finding about real behavior, a validation of the research instrument, or an estimate of model performance on the real study. The original study data cannot be distributed.

## What this demonstrates

| Topic | Where (notebook section) |
|---|---|
| Schema and arithmetic assertions that fail loudly | 1 |
| Target-leakage audit (including leakage through missingness) | 1, 2 |
| Redundancy audit: exact feature identities asserted, not assumed | 2 |
| Participant-grouped holdout and grouped CV (no row-level splitting) | 3 |
| Baselines: prior, two-feature logistic, full logistic, random forest, gradient boosting | 3 |
| Repeated grouped CV, ROC-AUC and Brier score alongside macro-F1 | 3 |
| Hyperparameter search restricted to training participants | 4 |
| Within-participant label-permutation test against chance | 5 |
| One-time holdout evaluation with participant-level bootstrap intervals | 6 |
| Error analysis by participant | 6 |
| Permutation importance with uncertainty | 7 |
| Unsupervised exploration with train-only fitting and holdout projection | 8 |
| Explicit limits and extensions | 9 |

The task is retrospective classification of trial success from aggregate gaze/interaction features in a single phase, generalizing to unseen participants. High accuracy is neither expected nor the goal: the synthetic signal is weak by design, and the notebook is written to show what an honest analysis of weak signal looks like.

## Repository layout

```
data/synthetic_portal_trials.csv        synthetic trial-level data (24 participants, 384 trials, 72 columns)
notebooks/Behavioral_ML_Methods_Demo.ipynb   the analysis, committed with outputs
src/generate_synthetic_data.py          data generator (see "Synthetic data" below)
requirements.txt
```

## Run it

```bash
pip install -r requirements.txt
jupyter lab notebooks/Behavioral_ML_Methods_Demo.ipynb
```

The notebook finds the data whether it is opened from the repository root or from `notebooks/`. All randomness is seeded (`SEED = 2026`). A full run takes about two minutes on a laptop, most of it in the permutation test (section 5).

Committed outputs were produced with Python 3.12, scikit-learn 1.9, pandas 3.0, NumPy 2.5 and Matplotlib 3.11. Numbers can shift slightly across library versions.

## Synthetic data
Due to license regulation, data ownership, and the IP rules of the original dataset, this repository demonstrates the pipeline design and deployment using synthetic dataset that follows the exact data structure in the actual project. The synthetic data was generated using "generate_synthetic_data.py" script.  

## Limits

See section 9 of the notebook. In short: synthetic relationships carry no external validity, the cohort is small, grouped validation does not address site or hardware differences, and the task is retrospective rather than a real-time predictor.
