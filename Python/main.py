import pandas as pd
import matplotlib.pyplot as plt

# Press the green button in the gutter to run the script.
if __name__ == '__main__':
    plt.rcParams["figure.figsize"] = [7.50, 3.50]
    plt.rcParams["figure.autolayout"] = True

    headers = ['generation', 'average_fitness', 'best_fitness', 'best_hidden_nodes', 'best_nr_connections']
    # headers = ['generation', 'average_fitness', 'best_fitness']

    fileName = r"E:\Faculatate\Anul3\Licenta\PredatorPreyEcosystem2d\Logs\Fitness\fitness_log_638220373417462089.csv"
    df = pd.read_csv(fileName, sep=",", decimal=".", index_col=0)
    # df['generation'] = df['generation'].astype(int)
    # df['average_fitness'] = df['average_fitness'].astype(float)
    # df['best_fitness'] = df['best_fitness'].astype(float)
    # print(df.dtypes)
    # df[['generation', 'average_fitness', 'best_fitness']].plot()
    df[['best_fitness']].plot()
    df[['average_fitness']].plot()
    df[['best_hidden_nodes']].plot()
    df[['best_nr_connections']].plot()

    #
    plt.show()

# See PyCharm help at https://www.jetbrains.com/help/pycharm/
