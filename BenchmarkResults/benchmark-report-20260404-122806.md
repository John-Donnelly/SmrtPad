# SmrtPad AI Model Benchmark Report

**Generated:** 2026-04-04 12:28:06 UTC
**Duration:** 00:18:06
**Models tested:** 1
**Prompts per model:** 38
**Total runs:** 38

## Model Summary

| Model | Target | Avg Score | Avg TPS | Success Rate | Avg Cost/Req | Total Time |
|-------|--------|-----------|---------|--------------|--------------|------------|
| phi-4-mini | GPU | 0.880 | 50.0 | 97 % | $0.000077 | 742s |

## Per-Skill Breakdown

| Skill | Avg Score | Avg TPS | Best Model | Worst Model |
|-------|-----------|---------|------------|-------------|
| autocomplete | 0.842 | 52.0 | phi-4-mini | phi-4-mini |
| freeform | 1.000 | 45.8 | phi-4-mini | phi-4-mini |
| grammar | 0.854 | 52.4 | phi-4-mini | phi-4-mini |
| ocr | 0.854 | 40.3 | phi-4-mini | phi-4-mini |
| rewrite | 0.882 | 54.1 | phi-4-mini | phi-4-mini |
| semantic | 0.981 | 53.6 | phi-4-mini | phi-4-mini |
| shorten | 0.839 | 49.6 | phi-4-mini | phi-4-mini |
| summarize | 0.889 | 49.0 | phi-4-mini | phi-4-mini |
| tone-casual | 0.853 | 52.6 | phi-4-mini | phi-4-mini |
| tone-professional | 0.816 | 46.6 | phi-4-mini | phi-4-mini |

## Detailed Results

| Prompt | Model | Score | TPS | Output Tokens | Time (s) | Cost | Notes |
|--------|-------|-------|-----|---------------|----------|------|-------|
| summarize-01 | phi-4-mini | 0.915 | 33.6 | 58 | 15.2 | $0.000058 |  |
| summarize-02 | phi-4-mini | 0.881 | 54.6 | 41 | 19.1 | $0.000073 |  |
| summarize-03 | phi-4-mini | 0.881 | 53.9 | 38 | 19.1 | $0.000073 |  |
| summarize-04 | phi-4-mini | 0.881 | 54.0 | 40 | 19.1 | $0.000073 |  |
| tone-professional-01 | phi-4-mini | 0.854 | 52.5 | 24 | 24.1 | $0.000093 |  |
| tone-professional-02 | phi-4-mini | 0.824 | 51.7 | 18 | 24.1 | $0.000092 | Output too short (18 < 20 min) |
| tone-professional-03 | phi-4-mini | 0.734 | 29.6 | 9 | 24.1 | $0.000093 | Output too short (9 < 15 min) |
| tone-professional-04 | phi-4-mini | 0.853 | 52.4 | 19 | 24.2 | $0.000093 |  |
| tone-casual-01 | phi-4-mini | 0.854 | 51.6 | 25 | 24.1 | $0.000092 |  |
| tone-casual-02 | phi-4-mini | 0.853 | 52.0 | 20 | 24.3 | $0.000093 |  |
| tone-casual-03 | phi-4-mini | 0.852 | 54.8 | 38 | 24.4 | $0.000094 |  |
| tone-casual-04 | phi-4-mini | 0.853 | 52.2 | 19 | 24.3 | $0.000093 |  |
| rewrite-01 | phi-4-mini | 0.881 | 54.7 | 27 | 19.0 | $0.000073 |  |
| rewrite-02 | phi-4-mini | 0.881 | 52.4 | 20 | 19.0 | $0.000073 |  |
| rewrite-03 | phi-4-mini | 0.884 | 55.0 | 33 | 18.6 | $0.000071 |  |
| rewrite-04 | phi-4-mini | 0.881 | 54.3 | 27 | 19.0 | $0.000073 |  |
| grammar-01 | phi-4-mini | 0.854 | 52.8 | 22 | 24.1 | $0.000092 |  |
| grammar-02 | phi-4-mini | 0.853 | 49.5 | 16 | 24.2 | $0.000093 |  |
| grammar-03 | phi-4-mini | 0.853 | 53.4 | 23 | 24.2 | $0.000093 |  |
| grammar-04 | phi-4-mini | 0.854 | 53.9 | 24 | 24.1 | $0.000092 |  |
| shorten-01 | phi-4-mini | 0.854 | 44.8 | 36 | 24.0 | $0.000092 |  |
| shorten-02 | phi-4-mini | 0.853 | 54.4 | 32 | 24.2 | $0.000093 |  |
| shorten-03 | phi-4-mini | 0.793 | 45.9 | 9 | 24.2 | $0.000093 | Shortened text not significantly shorter |
| shorten-04 | phi-4-mini | 0.853 | 53.4 | 39 | 24.2 | $0.000093 |  |
| autocomplete-01 | phi-4-mini | 0.852 | 50.7 | 18 | 24.4 | $0.000094 |  |
| autocomplete-02 | phi-4-mini | 0.851 | 54.9 | 38 | 24.8 | $0.000095 |  |
| autocomplete-03 | phi-4-mini | 0.813 | 50.3 | 13 | 24.3 | $0.000093 | Output too short (13 < 15 min) |
| autocomplete-04 | phi-4-mini | 0.852 | 52.1 | 18 | 24.6 | $0.000094 |  |
| semantic-01 | phi-4-mini | 0.942 | 56.2 | 254 | 13.0 | $0.000050 |  |
| semantic-02 | phi-4-mini | 1.000 | 54.9 | 48 | 8.5 | $0.000033 |  |
| semantic-03 | phi-4-mini | 1.000 | 49.8 | 103 | 7.5 | $0.000029 |  |
| ocr-01 | phi-4-mini | 0.854 | 30.2 | 16 | 24.1 | $0.000092 |  |
| ocr-02 | phi-4-mini | 0.853 | 50.4 | 16 | 24.2 | $0.000093 |  |
| freeform-01 | phi-4-mini | N/A | 0.0 | 0 | 0.0 | N/A | The specified element ID is either null or the empty string. |
| freeform-02 | phi-4-mini | 1.000 | 33.9 | 11 | 5.6 | $0.000022 |  |
| freeform-03 | phi-4-mini | 1.000 | 56.4 | 68 | 9.2 | $0.000035 |  |
| freeform-04 | phi-4-mini | 1.000 | 56.3 | 56 | 7.9 | $0.000030 |  |
| freeform-05 | phi-4-mini | 1.000 | 36.5 | 16 | 8.8 | $0.000034 |  |
