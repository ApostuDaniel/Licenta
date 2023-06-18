import pandas as pd
import matplotlib.pyplot as plt


def make_twin_plot(df, field1, field2):
    fig, ax = plt.subplots()
    ax.set_ylabel(field1)
    ax2 = ax.twinx()
    ax2.set_ylabel(field2)
    df[field1].plot(ax=ax)
    df[field2].plot(ax=ax2, color='r')
    return fig


# Press the green button in the gutter to run the script.
if __name__ == '__main__':
    plt.rcParams["figure.figsize"] = [7.50, 3.50]
    plt.rcParams["figure.autolayout"] = True

    headers = ['generation', 'average_fitness', 'best_fitness', 'best_hidden_nodes', 'best_nr_connections']
    # headers = ['generation', 'average_fitness', 'best_fitness']

    fileName = r"E:\Faculatate\Anul3\Licenta\PredatorPreyEcosystem2d\Logs\Fitness\fitness_log_638227166565336427.csv"
    df = pd.read_csv(fileName, sep=",", decimal=".", index_col=0)
    # df['generation'] = df['generation'].astype(int)
    # df['average_fitness'] = df['average_fitness'].astype(float)
    # df['best_fitness'] = df['best_fitness'].astype(float)
    # print(df.dtypes)
    # df[['generation', 'average_fitness', 'best_fitness']].plot()

    # fig, ax = plt.subplots()
    # ax2 = ax.twinx()
    # df.best_fitness.plot(ax=ax)
    # df.best_nr_connections.plot(ax=ax2, color='r')
    # plt.show()

    fig = make_twin_plot(df, 'best_fitness', 'best_nr_connections')
    fig2 = make_twin_plot(df, 'best_fitness', 'average_fitness')
    plt.show()

# See PyCharm help at https://www.jetbrains.com/help/pycharm/
